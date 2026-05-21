using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Services
{
    public class AIAssistantService : IAIAssistantService
    {
        private readonly ApplicationDbContext         _context;
        private readonly IAIPermissionService         _permissions;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AIAssistantService>  _logger;
        private readonly IHttpClientFactory           _httpFactory;
        private readonly IMemoryCache                 _cache;
        private readonly string                       _systemApiKey;
        private readonly string                       _openAiApiKey;
        private readonly string                       _groqApiKey;

        private const string AnthropicModel  = "claude-sonnet-4-6";
        private const string GroqBaseUrl     = "https://api.groq.com/openai/v1";
        private const string OpenAIBaseUrl   = "https://api.openai.com/v1";
        private const int    HistoryPairs   = 3;
        private const string ConfigCacheKey = "AI:SystemConfig";

        public AIAssistantService(
            ApplicationDbContext context,
            IAIPermissionService permissions,
            UserManager<ApplicationUser> userManager,
            ILogger<AIAssistantService> logger,
            IHttpClientFactory httpFactory,
            IMemoryCache cache,
            IConfiguration configuration)
        {
            _context      = context;
            _permissions  = permissions;
            _userManager  = userManager;
            _logger       = logger;
            _httpFactory  = httpFactory;
            _cache        = cache;
            // Fallback keys from appsettings.json (DB keys take priority at call time)
            _systemApiKey = configuration["Anthropic:ApiKey"] ?? "";
            _openAiApiKey = configuration["OpenAI:ApiKey"] ?? "";
            _groqApiKey   = configuration["Groq:ApiKey"]   ?? "";
        }

        // Returns DB key if set, otherwise falls back to appsettings.json value
        private string ResolveApiKey(string? dbKey, string fallbackKey)
            => !string.IsNullOrWhiteSpace(dbKey) ? dbKey : fallbackKey;

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public async Task<AIChatMessage> AskQuestionAsync(string userId, string question,
            string? ipAddress, string? userAgent)
        {
            var cfg = await GetSystemConfigAsync();

            if (!cfg.IsEnabled)
                return await SaveFailedAsync(userId, question,
                    "المساعد الذكي معطّل حالياً من قِبَل إدارة النظام.", null);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return await SaveFailedAsync(userId, question, "المستخدم غير موجود.", null);

            var roles = await _userManager.GetRolesAsync(user);
            var role  = roles.FirstOrDefault() ?? "Trainee";
            var perms = await _permissions.GetRolePermissionsAsync(role);
            var limit = perms?.DailyQueryLimit ?? 50;

            if (!await _permissions.IsSystemWithinDailyLimitAsync(cfg.SystemDailyLimit))
            {
                await _permissions.LogAccessAsync(userId, "READ", "AI", null, false, "حد النظام اليومي", ipAddress, userAgent);
                return await SaveFailedAsync(userId, question,
                    "المساعد الذكي وصل إلى الحد اليومي الأقصى للنظام. يرجى المحاولة غداً.", role);
            }

            if (!await _permissions.IsWithinDailyLimitAsync(userId, limit))
            {
                await _permissions.LogAccessAsync(userId, "READ", "AI", null, false, "حد يومي", ipAddress, userAgent);
                return await SaveFailedAsync(userId, question,
                    $"وصلت إلى حد الأسئلة اليومي ({(limit < 0 ? "غير محدود" : limit.ToString())} سؤال). يمكنك المحاولة غداً.", role);
            }

            try
            {
                var systemPrompt = await BuildEnrichedSystemPromptAsync(user, role);
                var history      = await LoadHistoryAsync(userId);

                string aiResponse;
                string providerLabel;

                // Schema guide: describes available tools without injecting actual data.
                // The AI requests data via tool calling on demand — more private and accurate.
                var schemaGuide    = BuildSchemaGuide(role);
                var enrichedPrompt = systemPrompt + schemaGuide;

                // Resolve keys: DB value takes priority over appsettings.json
                var anthropicKey = ResolveApiKey(cfg.AnthropicApiKey, _systemApiKey);
                var openAiKey    = ResolveApiKey(cfg.OpenAIApiKey,    _openAiApiKey);
                var groqKey      = ResolveApiKey(cfg.GroqApiKey,      _groqApiKey);

                bool needsData = IsDataQuestion(question);

                if (cfg.Provider == AIProviderType.Ollama)
                {
                    aiResponse    = await CallOllamaWithGuidedToolsAsync(
                        enrichedPrompt, history, question,
                        cfg.OllamaUrl, cfg.OllamaModel, userId, role);
                    providerLabel = $"Ollama ({cfg.OllamaModel})";
                }
                else if (cfg.Provider == AIProviderType.Groq)
                {
                    aiResponse = needsData
                        ? await CallOpenAIWithToolsAsync(enrichedPrompt, history, question,
                              groqKey, cfg.GroqModel, cfg.MaxTokensPerResponse, userId, role,
                              GroqBaseUrl)
                        : await CallOpenAIAsync(enrichedPrompt, history, question,
                              groqKey, cfg.GroqModel, cfg.MaxTokensPerResponse,
                              GroqBaseUrl);
                    providerLabel = $"Groq ({cfg.GroqModel})";
                }
                else if (cfg.Provider == AIProviderType.OpenAI)
                {
                    aiResponse = needsData
                        ? await CallOpenAIWithToolsAsync(enrichedPrompt, history, question,
                              openAiKey, cfg.OpenAIModel, cfg.MaxTokensPerResponse, userId, role,
                              OpenAIBaseUrl)
                        : await CallOpenAIAsync(enrichedPrompt, history, question,
                              openAiKey, cfg.OpenAIModel, cfg.MaxTokensPerResponse,
                              OpenAIBaseUrl);
                    providerLabel = $"OpenAI ({cfg.OpenAIModel})";
                }
                else
                {
                    aiResponse = needsData
                        ? await CallAnthropicWithToolsAsync(enrichedPrompt, history, question,
                              anthropicKey, cfg.MaxTokensPerResponse, userId, role)
                        : await CallAnthropicAsync(enrichedPrompt, history, question,
                              anthropicKey, cfg.MaxTokensPerResponse);
                    providerLabel = "Claude (Anthropic)";
                }

                var msg = new AIChatMessage
                {
                    UserId        = userId,
                    UserMessage   = question,
                    AIResponse    = aiResponse,
                    QuestionType  = ClassifyQuestion(question),
                    IsAnswered    = true,
                    UserRole      = role,
                    DataAccessLog = providerLabel,
                    AnsweredAt    = DateTime.UtcNow
                };
                _context.AIChatMessages.Add(msg);
                await _context.SaveChangesAsync();

                await _permissions.LogAccessAsync(userId, "READ", providerLabel, null, true, null, ipAddress, userAgent);
                return msg;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI call failed [{Type}] for user {UserId}: {Message}",
                    ex.GetType().Name, userId, ex.Message);
                var msg = ex.Message;
                var userMsg = msg.Contains("401") || msg.Contains("Unauthorized")
                    ? "المفتاح غير صالح أو منتهي الصلاحية. يرجى تحديث مفتاح API من إعدادات المساعد الذكي."
                    : msg.Contains("429") || msg.Contains("rate_limit") || msg.Contains("rate limit")
                        ? "تجاوزت الحد اليومي لـ API. يرجى المحاولة لاحقاً أو تغيير المزود."
                        : msg.Contains("400") || msg.Contains("BadRequest")
                            ? $"طلب غير صالح للنموذج: {msg}"
                            : $"خطأ [{ex.GetType().Name}]: {msg}";
                return await SaveFailedAsync(userId, question, userMsg, role);
            }
        }

        private async Task<AISystemConfig> GetSystemConfigAsync()
        {
            if (_cache.TryGetValue(ConfigCacheKey, out AISystemConfig? cached) && cached != null)
                return cached;

            var config = await _context.AISystemConfigs.AsNoTracking().FirstOrDefaultAsync()
                         ?? new AISystemConfig();

            _cache.Set(ConfigCacheKey, config, TimeSpan.FromMinutes(5));
            return config;
        }

        public async Task<List<AIChatMessage>> GetChatHistoryAsync(string userId,
            int pageNumber = 1, int pageSize = 20)
            => await _context.AIChatMessages
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        public async Task<bool> RateResponseAsync(Guid messageId, string userId, int rating, string? feedback)
        {
            if (rating < 1 || rating > 5) return false;
            var msg = await _context.AIChatMessages.FindAsync(messageId);
            if (msg == null || msg.UserId != userId) return false;
            msg.Rating       = rating;
            msg.UserFeedback = feedback;
            msg.IsHelpful    = rating >= 4;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<AIStatistics> GetStatisticsAsync(string userId)
        {
            var msgs = await _context.AIChatMessages.Where(m => m.UserId == userId).ToListAsync();
            return new AIStatistics
            {
                TotalQuestions    = msgs.Count,
                AnsweredQuestions = msgs.Count(m => m.IsAnswered),
                AverageRating     = msgs.Where(m => m.Rating.HasValue)
                                        .Select(m => (double)m.Rating!.Value)
                                        .DefaultIfEmpty(0).Average(),
                MostAskedCategory = msgs.GroupBy(m => m.QuestionType)
                                        .OrderByDescending(g => g.Count())
                                        .Select(g => g.Key.ToString())
                                        .FirstOrDefault() ?? "—",
                LastQuestion      = msgs.OrderByDescending(m => m.CreatedAt)
                                        .Select(m => (DateTime?)m.CreatedAt)
                                        .FirstOrDefault()
            };
        }

        public async Task<bool> DeleteMessageAsync(Guid messageId, string userId)
        {
            var msg = await _context.AIChatMessages.FindAsync(messageId);
            if (msg == null || msg.UserId != userId) return false;
            msg.IsDeleted = true;
            msg.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Layer 1 — Direct Response Engine
        //  Returns a fully-formatted Arabic answer built from DB data.
        //  Returns null when the question is conversational → falls through to LLM.
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string?> TryDirectResponseAsync(
            string userId, string role, ApplicationUser user, string question)
        {
            // Use Unicode-safe Contains — no ToLowerInvariant since Arabic has no case.
            // Use short root substrings so morphological variants all match.
            // e.g. "دور" matches دورة / دورات / دوراتي / الدورة
            var q = question;

            bool isCourseQ  = q.Contains("دور")   || q.Contains("كورس")   || q.Contains("مقرر")  || q.Contains("تسجيل")  || q.Contains("course",      StringComparison.OrdinalIgnoreCase);
            bool isAttendQ  = q.Contains("حضو")   || q.Contains("غياب")   || q.Contains("حضرت") || q.Contains("محاضرة") || q.Contains("attend",      StringComparison.OrdinalIgnoreCase);
            bool isExamQ    = q.Contains("امتحا") || q.Contains("نتيج")   || q.Contains("درجة") || q.Contains("علامة")  || q.Contains("ناجح")  || q.Contains("راسب") || q.Contains("اختبا") || q.Contains("exam", StringComparison.OrdinalIgnoreCase);
            bool isPayQ     = q.Contains("دفع")   || q.Contains("فاتور")  || q.Contains("مبلغ") || q.Contains("رسوم")   || q.Contains("سدد")   || q.Contains("ديون") || q.Contains("دفعت") || q.Contains("payment", StringComparison.OrdinalIgnoreCase);
            bool isSessionQ = q.Contains("جلسة")  || q.Contains("مباشر")  || q.Contains("لايف") || q.Contains("موعد")   || q.Contains("بث")    || q.Contains("session",     StringComparison.OrdinalIgnoreCase);
            bool isCertQ    = q.Contains("شهادة") || q.Contains("شهادا")  || q.Contains("certificate", StringComparison.OrdinalIgnoreCase);
            bool isStatsQ   = q.Contains("إحصا")  || q.Contains("عدد ال") || q.Contains("تقرير") || q.Contains("إيراد") || q.Contains("مجموع") || q.Contains("كم ") || q.Contains("statistics", StringComparison.OrdinalIgnoreCase);

            _logger.LogDebug("DirectEngine role={Role} course={C} attend={A} exam={E} pay={P} session={S} cert={Cert} stats={St}",
                role, isCourseQ, isAttendQ, isExamQ, isPayQ, isSessionQ, isCertQ, isStatsQ);

            switch (role)
            {
                case "Trainee":
                    if (isCourseQ)  return await DirectTraineeCoursesAsync(userId, user);
                    if (isAttendQ)  return await DirectTraineeAttendanceAsync(userId, user);
                    if (isExamQ)    return await DirectTraineeExamsAsync(userId, user);
                    if (isPayQ)     return await DirectTraineePaymentsAsync(userId, user);
                    if (isCertQ)    return await DirectTraineeCertificatesAsync(userId, user);
                    if (isSessionQ) return await DirectTraineeSessionsAsync(userId, user);
                    break;

                case "Trainer":
                    if (isCourseQ || isStatsQ) return await DirectTrainerCoursesAsync(userId, user);
                    if (isSessionQ)            return await DirectTrainerSessionsAsync(userId, user);
                    if (isExamQ)               return await DirectTrainerExamsAsync(userId, user);
                    break;

                case "Admin":
                    // Exclude recommendation/advice questions — let LLM handle those
                    bool isAdvice = q.Contains("أنصح") || q.Contains("انصح") || q.Contains("اقترح")
                                 || q.Contains("أفضل") || q.Contains("أهم")  || q.Contains("افضل");
                    if ((isStatsQ || isPayQ) && !isAdvice)
                        return await DirectAdminStatsAsync(user);
                    if (isExamQ && !isAdvice)
                        return await DirectAdminExamStatsAsync(user);
                    break;

                case "Receptionist":
                    bool isAdviceR = q.Contains("أنصح") || q.Contains("انصح") || q.Contains("اقترح");
                    if ((isPayQ || isStatsQ) && !isAdviceR)
                        return await DirectReceptionistSummaryAsync(user);
                    break;
            }

            return null;
        }

        // ── Trainee direct responses ──────────────────────────────────────────

        private async Task<string> DirectTraineeCoursesAsync(string userId, ApplicationUser user)
        {
            var trainee = await _context.Trainees
                .Include(t => t.CourseTrainees).ThenInclude(ct => ct.Course)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (trainee == null)
                return $"مرحباً {user.FullName}،\n\nلم يُعثر على سجل متدرب مرتبط بحسابك. يرجى التواصل مع الإدارة.";

            var courses = trainee.CourseTrainees
                .Where(ct => ct.Course != null && !ct.Course.IsDeleted)
                .ToList();

            if (!courses.Any())
                return $"مرحباً {user.FullName}،\n\nأنت غير مسجل في أي دورة حالياً. للتسجيل، تواصل مع موظف الاستقبال.";

            var sb = new StringBuilder();
            sb.AppendLine($"مرحباً {user.FullName}!");
            sb.AppendLine($"\nدوراتك المسجلة في مركز التدريب ({courses.Count} دورة):\n");
            for (int i = 0; i < courses.Count; i++)
            {
                var ct = courses[i];
                sb.AppendLine($"{i + 1}. {ct.Course.CourseName}");
                sb.AppendLine($"   الدفعة: {ct.Course.BatchNumber}");
                sb.AppendLine($"   الرسوم: {ct.Course.Price:N0} {ct.Course.CourseCurrency}");
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        private async Task<string> DirectTraineeAttendanceAsync(string userId, ApplicationUser user)
        {
            var trainee = await _context.Trainees
                .Include(t => t.CourseTrainees).ThenInclude(ct => ct.Course)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (trainee == null)
                return $"مرحباً {user.FullName}، لم يُعثر على سجل متدرب مرتبط بحسابك.";

            var courses = trainee.CourseTrainees
                .Where(ct => ct.Course != null && !ct.Course.IsDeleted)
                .ToList();

            if (!courses.Any())
                return $"مرحباً {user.FullName}، أنت غير مسجل في أي دورة حتى الآن.";

            var sb = new StringBuilder();
            sb.AppendLine($"مرحباً {user.FullName}! إليك سجل حضورك:\n");

            foreach (var ct in courses)
            {
                var total    = await _context.Lectures.CountAsync(l => l.CourseId == ct.CourseId && !l.IsDeleted);
                var attended = await _context.Presences.CountAsync(p =>
                    p.TraineeId == trainee.TraineeId && p.Lecture.CourseId == ct.CourseId && p.IsPresent);
                var pct = total > 0 ? attended * 100.0 / total : 0;
                var status = pct >= 75 ? "ممتاز" : pct >= 60 ? "مقبول" : "تحذير — نسبة منخفضة";
                sb.AppendLine($"• {ct.Course.CourseName}:");
                sb.AppendLine($"  حضرت {attended} من {total} محاضرة — النسبة: {pct:F1}% ({status})");
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        private async Task<string> DirectTraineeExamsAsync(string userId, ApplicationUser user)
        {
            var trainee = await _context.Trainees.AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null)
                return $"مرحباً {user.FullName}، لم يُعثر على سجل متدرب مرتبط بحسابك.";

            var attempts = await _context.ExamAttempts
                .Include(a => a.Exam)
                .Where(a => a.TraineeId == trainee.TraineeId && a.SubmittedAt != null)
                .OrderByDescending(a => a.SubmittedAt)
                .Take(15)
                .AsNoTracking()
                .ToListAsync();

            if (!attempts.Any())
                return $"مرحباً {user.FullName}، لم تُؤدِّ أي امتحانات حتى الآن.";

            var sb = new StringBuilder();
            sb.AppendLine($"مرحباً {user.FullName}! إليك نتائج امتحاناتك ({attempts.Count} امتحان):\n");

            int passed = attempts.Count(a => a.IsPassed == true);
            int failed = attempts.Count(a => a.IsPassed == false);

            foreach (var a in attempts)
            {
                var result = a.IsPassed == true ? "ناجح ✓" : "راسب ✗";
                sb.AppendLine($"• {a.Exam.ExamName}: {a.TotalScore:F1} نقطة — {result}");
            }
            sb.AppendLine();
            sb.AppendLine($"الملخص: {passed} ناجح، {failed} راسب من إجمالي {attempts.Count} امتحان.");
            return sb.ToString().TrimEnd();
        }

        private async Task<string> DirectTraineePaymentsAsync(string userId, ApplicationUser user)
        {
            var trainee = await _context.Trainees
                .Include(t => t.Payments.Where(p => !p.IsDeleted))
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (trainee == null)
                return $"مرحباً {user.FullName}، لم يُعثر على سجل متدرب مرتبط بحسابك.";

            var pmts = trainee.Payments.OrderByDescending(p => p.CreatedDate).ToList();

            if (!pmts.Any())
                return $"مرحباً {user.FullName}، لا توجد سجلات دفع في حسابك حتى الآن.";

            var sb = new StringBuilder();
            sb.AppendLine($"مرحباً {user.FullName}! إليك سجل مدفوعاتك:\n");
            for (int i = 0; i < pmts.Count; i++)
            {
                var p = pmts[i];
                sb.AppendLine($"{i + 1}. المبلغ: {p.TotalAmount:N0} — التاريخ: {p.CreatedDate:dd/MM/yyyy}");
            }
            sb.AppendLine();
            sb.AppendLine($"إجمالي المدفوع: {pmts.Sum(p => p.TotalAmount):N0}");
            return sb.ToString().TrimEnd();
        }

        private async Task<string> DirectTraineeCertificatesAsync(string userId, ApplicationUser user)
        {
            var trainee = await _context.Trainees
                .Include(t => t.Certificates).ThenInclude(c => c.Course)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (trainee == null)
                return $"مرحباً {user.FullName}، لم يُعثر على سجل متدرب مرتبط بحسابك.";

            var certs = trainee.Certificates.Where(c => !c.IsDeleted).ToList();
            if (!certs.Any())
                return $"مرحباً {user.FullName}، لم تحصل على أي شهادات حتى الآن. أكمل دوراتك لتحصل عليها.";

            var sb = new StringBuilder();
            sb.AppendLine($"مرحباً {user.FullName}! حصلت على {certs.Count} شهادة:\n");
            foreach (var c in certs)
            {
                var courseName = c.Course?.CourseName ?? "—";
                sb.AppendLine($"• شهادة دورة: {courseName} (معدل: {c.Average:F1}%)");
            }
            return sb.ToString().TrimEnd();
        }

        private async Task<string> DirectTraineeSessionsAsync(string userId, ApplicationUser user)
        {
            var trainee = await _context.Trainees
                .Include(t => t.CourseTrainees)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (trainee == null)
                return $"مرحباً {user.FullName}، لم يُعثر على سجل متدرب مرتبط بحسابك.";

            var courseIds = trainee.CourseTrainees.Select(ct => ct.CourseId).ToList();
            var sessions  = await _context.LiveSessions
                .Include(ls => ls.Course)
                .Where(ls => courseIds.Contains(ls.CourseId)
                          && !ls.IsCancelled
                          && ls.ScheduledAt >= DateTime.UtcNow.AddMinutes(-60))
                .OrderBy(ls => ls.ScheduledAt)
                .Take(10)
                .AsNoTracking()
                .ToListAsync();

            if (!sessions.Any())
                return $"مرحباً {user.FullName}، لا توجد جلسات مباشرة قادمة لدوراتك حالياً.";

            var sb = new StringBuilder();
            sb.AppendLine($"مرحباً {user.FullName}! الجلسات المباشرة القادمة لك:\n");
            foreach (var ls in sessions)
                sb.AppendLine($"• {ls.Title} ({ls.Course?.CourseName ?? "—"}) — {ls.ScheduledAt.ToLocalTime():dd/MM/yyyy hh:mm tt}");
            return sb.ToString().TrimEnd();
        }

        // ── Trainer direct responses ──────────────────────────────────────────

        private async Task<string> DirectTrainerCoursesAsync(string userId, ApplicationUser user)
        {
            var trainer = await _context.Trainers
                .Include(t => t.CourseTrainers).ThenInclude(ct => ct.Course)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (trainer == null)
                return $"مرحباً {user.FullName}، لم يُعثر على سجل مدرب مرتبط بحسابك.";

            if (!trainer.CourseTrainers.Any())
                return $"مرحباً {user.FullName}، لا توجد دورات مسندة إليك حالياً.";

            var sb = new StringBuilder();
            sb.AppendLine($"مرحباً {user.FullName}! الدورات التي تُدرِّسها ({trainer.CourseTrainers.Count} دورة):\n");
            foreach (var ct in trainer.CourseTrainers)
            {
                var cnt = await _context.CourseTrainees.CountAsync(c => c.CourseId == ct.CourseId);
                sb.AppendLine($"• {ct.Course.CourseName} — {cnt} متدرب مسجل");
            }
            return sb.ToString().TrimEnd();
        }

        private async Task<string> DirectTrainerSessionsAsync(string userId, ApplicationUser user)
        {
            var sessions = await _context.LiveSessions
                .Include(ls => ls.Course)
                .Where(ls => ls.CreatedByUserId == userId && !ls.IsCancelled
                          && ls.ScheduledAt >= DateTime.UtcNow)
                .OrderBy(ls => ls.ScheduledAt)
                .Take(10)
                .AsNoTracking()
                .ToListAsync();

            if (!sessions.Any())
                return $"مرحباً {user.FullName}، لا توجد جلسات مباشرة مجدولة لك حالياً.";

            var sb = new StringBuilder();
            sb.AppendLine($"مرحباً {user.FullName}! جلساتك المباشرة القادمة ({sessions.Count}):\n");
            foreach (var ls in sessions)
                sb.AppendLine($"• {ls.Title} ({ls.Course?.CourseName ?? "—"}) — {ls.ScheduledAt.ToLocalTime():dd/MM/yyyy hh:mm tt}");
            return sb.ToString().TrimEnd();
        }

        private async Task<string> DirectTrainerExamsAsync(string userId, ApplicationUser user)
        {
            var count = await _context.Exams
                .Where(e => e.Trainer != null && e.Trainer.UserId == userId)
                .CountAsync();
            return $"مرحباً {user.FullName}، لقد أنشأت {count} امتحان حتى الآن.";
        }

        // ── Admin/Receptionist direct responses ───────────────────────────────

        private async Task<string> DirectAdminStatsAsync(ApplicationUser user)
        {
            var trainees       = await _context.Trainees.CountAsync();
            var femaleTrainees = await _context.Users.CountAsync(u => u.Role == RoleType.Trainee && u.Gender == Gender.Female);
            var trainers       = await _context.Trainers.CountAsync();
            var femaleTrainers = await _context.Users.CountAsync(u => u.Role == RoleType.Trainer && u.Gender == Gender.Female);
            var courses        = await _context.Courses.CountAsync(c => !c.IsDeleted);
            var exams          = await _context.Exams.CountAsync();
            var revenue        = await _context.Payments.Where(p => !p.IsDeleted).SumAsync(p => p.TotalAmount);
            var sessions       = await _context.LiveSessions.CountAsync(ls => !ls.IsCancelled && ls.ScheduledAt >= DateTime.UtcNow);
            var certs          = await _context.Certificates.CountAsync();
            var aiToday        = await _context.AIChatMessages.CountAsync(m => m.IsAnswered && m.CreatedAt >= DateTime.UtcNow.Date);
            var pendingPay     = await _context.PaymentRequests.CountAsync(r => r.Status == PaymentRequestStatus.Pending);

            return $"""
                مرحباً {user.FullName}! إليك إحصائيات المركز:

                المتدربون : {trainees} ({trainees - femaleTrainees} ذكور، {femaleTrainees} إناث)
                المدربون  : {trainers} ({trainers - femaleTrainers} ذكور، {femaleTrainers} إناث)
                الدورات النشطة     : {courses}
                الامتحانات         : {exams}
                الشهادات الصادرة   : {certs}
                الجلسات القادمة    : {sessions}
                إجمالي الإيرادات   : {revenue:N0}
                طلبات دفع معلقة   : {pendingPay}
                أسئلة AI اليوم    : {aiToday}
                """;
        }

        private async Task<string> DirectAdminExamStatsAsync(ApplicationUser user)
        {
            var total  = await _context.ExamAttempts.CountAsync(a => a.SubmittedAt != null);
            var passed = await _context.ExamAttempts.CountAsync(a => a.SubmittedAt != null && a.IsPassed == true);
            var exams  = await _context.Exams.CountAsync();
            var avg    = total > 0
                ? await _context.ExamAttempts.Where(a => a.SubmittedAt != null).AverageAsync(a => a.TotalScore)
                : 0;

            return $"""
                مرحباً {user.FullName}! بصفتك مسؤول النظام، لا يوجد سجل امتحانات شخصي لك.
                إليك إحصائيات امتحانات مركز التدريب:

                الامتحانات المُعدّة : {exams}
                إجمالي المحاولات   : {total}
                الناجحون           : {passed} ({(total > 0 ? passed * 100.0 / total : 0):F1}%)
                الراسبون           : {total - passed} ({(total > 0 ? (total - passed) * 100.0 / total : 0):F1}%)
                متوسط الدرجات     : {avg:F1}
                """;
        }

        private async Task<string> DirectReceptionistSummaryAsync(ApplicationUser user)
        {
            var trainees    = await _context.Trainees.CountAsync();
            var courses     = await _context.Courses.CountAsync(c => !c.IsDeleted);
            var todayPmts   = await _context.Payments.CountAsync(p => !p.IsDeleted && p.CreatedDate >= DateTime.UtcNow.Date);

            return $"""
                مرحباً {user.FullName}! إليك ملخص اليوم:

                إجمالي المتدربين           : {trainees}
                الدورات المتاحة            : {courses}
                المدفوعات المسجلة اليوم    : {todayPmts}
                """;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Universal DB Context — fetches all role-appropriate live data in
        //  parallel and returns a formatted Arabic block injected into every
        //  LLM provider's system prompt.  Enables ANY question to be answered.
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string> BuildRoleContextAsync(string userId, string role)
        {
            // DbContext is NOT thread-safe — queries must run sequentially, not via Task.WhenAll
            var parts = new List<string>();

            if (role is "Admin" or "Receptionist")
            {
                parts.Add(await ToolCenterStatisticsAsync());
                parts.Add(await ToolCourseDetailsAsync());
                parts.Add(await ToolPaymentBreakdownAsync());
                parts.Add(await ToolExamStatisticsAsync());
            }
            else if (role == "Trainee")
            {
                parts.Add(await ToolMyCoursesAsync(userId));
                parts.Add(await ToolMyAttendanceAsync(userId));
                parts.Add(await ToolMyExamResultsAsync(userId));
                parts.Add(await ToolMyPaymentsAsync(userId));
                parts.Add(await ToolMyCertificatesAsync(userId));
                parts.Add(await ToolMySessionsAsync(userId, role));
            }
            else if (role == "Trainer")
            {
                parts.Add(await ToolMyTeachingCoursesAsync(userId));
                parts.Add(await ToolMySessionsAsync(userId, role));
                parts.Add(await ToolMyExamStatsAsync(userId));
            }

            var sb = new StringBuilder();
            sb.AppendLine($"\n\n## بيانات قاعدة البيانات الحية ({DateTime.Now:dd/MM/yyyy HH:mm})");
            sb.AppendLine("استخدم هذه البيانات للإجابة على أي سؤال بدقة وبشكل طبيعي:\n");
            foreach (var r in parts)
            {
                sb.AppendLine(r);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // Returns true when the question likely needs database data.
        // Used only to decide whether to activate Tool Use for Claude/OpenAI
        // (the DB context is always injected regardless).
        private static bool IsDataQuestion(string q)
            => q.Contains("دور")   || q.Contains("كورس")  || q.Contains("حضو")    ||
               q.Contains("غياب")  || q.Contains("امتحا") || q.Contains("نتيج")   ||
               q.Contains("درجة")  || q.Contains("دفع")   || q.Contains("فاتور")  ||
               q.Contains("مبلغ")  || q.Contains("جلسة")  || q.Contains("مباشر")  ||
               q.Contains("شهادة") || q.Contains("إحصا")  || q.Contains("إيراد")  ||
               q.Contains("عدد ")  || q.Contains("كم ")   || q.Contains("ذكور")   ||
               q.Contains("إناث")  || q.Contains("أنثى")  || q.Contains("أنصح")   ||
               q.Contains("انصح")  || q.Contains("اقترح") || q.Contains("مستوا")  ||
               q.Contains("متدرب") || q.Contains("مدرب")  || q.Contains("مركز")   ||
               q.Contains("معدل")  || q.Contains("تقدم")  || q.Contains("أداء")   ||
               q.Contains("أفضل")  || q.Contains("أهم")   || q.Contains("مقارن")  ||
               q.Contains("ماذا")  || q.Contains("ما ال") || q.Contains("كيف ")   ||
               q.Contains("هل ")   || q.Contains("متى")   || q.Contains("بيان")   ||
               q.Contains("تقرير") || q.Contains("ملخص")  || q.Contains("مالي")   ||
               q.Contains("course", StringComparison.OrdinalIgnoreCase) ||
               q.Contains("exam",   StringComparison.OrdinalIgnoreCase) ||
               q.Contains("pay",    StringComparison.OrdinalIgnoreCase) ||
               q.Contains("stat",   StringComparison.OrdinalIgnoreCase);

        // ─────────────────────────────────────────────────────────────────────
        //  System prompt enriched with knowledge-base entries
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string> BuildEnrichedSystemPromptAsync(ApplicationUser user, string role)
        {
            var base_ = BuildCoreSystemPrompt(user, role);
            var kb = await _context.AIKnowledgeEntries
                .Where(e => e.IsActive)
                .OrderBy(e => e.SortOrder).ThenBy(e => e.CreatedAt)
                .AsNoTracking().Take(10)
                .ToListAsync();

            if (!kb.Any()) return base_;

            var kbSb = new StringBuilder("\n\n## معلومات المركز الثابتة (استخدمها كمرجع)\n");
            foreach (var g in kb.GroupBy(k => k.Category))
            {
                kbSb.AppendLine($"[{g.Key}]");
                foreach (var k in g) kbSb.AppendLine($"- {k.Title}: {k.Content}");
            }
            return base_ + kbSb;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Claude API — Tool Use / Function Calling (agentic loop)
        //
        //  Claude receives the raw question + tool definitions.
        //  It decides which tools to call, C# executes DB queries, results flow
        //  back to Claude for synthesis.  No keyword matching required.
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string> CallAnthropicWithToolsAsync(
            string systemPrompt,
            List<(string role, string content)> history,
            string question,
            string apiKey,
            int maxTokens,
            string userId,
            string userRole)
        {
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromMinutes(2);

            var toolDefs = BuildAnthropicTools(userRole);

            // Build conversation as JsonArray so we can append structured content
            var messages = new JsonArray();
            foreach (var (r, c) in history)
                messages.Add(new JsonObject { ["role"] = r, ["content"] = c });
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = question });

            for (int iter = 0; iter < 5; iter++)
            {
                var reqObj = new JsonObject
                {
                    ["model"]      = AnthropicModel,
                    ["max_tokens"] = maxTokens,
                    ["system"]     = systemPrompt,
                    ["tools"]      = JsonNode.Parse(JsonSerializer.Serialize(toolDefs))!,
                    ["messages"]   = messages.DeepClone()
                };

                var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
                {
                    Content = new StringContent(reqObj.ToJsonString(), Encoding.UTF8, "application/json")
                };
                req.Headers.Add("x-api-key",         apiKey);
                req.Headers.Add("anthropic-version", "2023-06-01");

                var resp = await http.SendAsync(req);
                var json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("Anthropic tool-use error {Status}: {Body}", resp.StatusCode, json);
                    throw new Exception($"Anthropic API error: {resp.StatusCode}");
                }

                using var doc      = JsonDocument.Parse(json);
                var stopReason     = doc.RootElement.GetProperty("stop_reason").GetString();
                var contentEl      = doc.RootElement.GetProperty("content");

                var textParts      = new List<string>();
                var toolCalls      = new List<(string id, string name, JsonElement input)>();
                var contentClone   = new JsonArray();

                foreach (var block in contentEl.EnumerateArray())
                {
                    contentClone.Add(JsonNode.Parse(block.GetRawText())!.DeepClone());
                    var t = block.GetProperty("type").GetString();
                    if (t == "text")
                        textParts.Add(block.GetProperty("text").GetString() ?? "");
                    else if (t == "tool_use")
                        toolCalls.Add((
                            block.GetProperty("id").GetString()!,
                            block.GetProperty("name").GetString()!,
                            block.GetProperty("input")));
                }

                if (stopReason == "end_turn" || !toolCalls.Any())
                {
                    var txt = string.Join("\n", textParts).Trim();
                    return string.IsNullOrWhiteSpace(txt) ? "لم أتمكن من توليد رد." : txt;
                }

                _logger.LogDebug("AI tools ({Count}): {Names}", toolCalls.Count, string.Join(", ", toolCalls.Select(t => t.name)));
                var toolPairs = new List<(string id, string result)>();
                foreach (var tc in toolCalls)
                    toolPairs.Add((tc.id, await ExecuteToolAsync(tc.name, tc.input, userId, userRole)));

                var toolResults = new JsonArray();
                foreach (var (id, result) in toolPairs)
                    toolResults.Add(new JsonObject
                    {
                        ["type"]        = "tool_result",
                        ["tool_use_id"] = id,
                        ["content"]     = result
                    });

                messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = contentClone.DeepClone() });
                messages.Add(new JsonObject { ["role"] = "user",      ["content"] = toolResults.DeepClone() });
            }
            return "عذراً، تعذّر الحصول على إجابة. يرجى المحاولة مجدداً.";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  OpenAI GPT — Direct call (conversational questions)
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string> CallOpenAIAsync(
            string systemPrompt,
            List<(string role, string content)> history,
            string question,
            string apiKey,
            string model,
            int maxTokens,
            string baseUrl       = OpenAIBaseUrl,
            int timeoutMinutes   = 2)
        {
            var http = _httpFactory.CreateClient();
            http.Timeout = timeoutMinutes <= 0
                ? Timeout.InfiniteTimeSpan
                : TimeSpan.FromMinutes(timeoutMinutes);

            var messages = new JsonArray();
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = systemPrompt });
            foreach (var (r, c) in history)
                messages.Add(new JsonObject { ["role"] = r == "user" ? "user" : "assistant", ["content"] = c });
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = question });

            var body = new JsonObject
            {
                ["model"]      = model,
                ["max_tokens"] = maxTokens,
                ["messages"]   = messages
            };

            var req = new HttpRequestMessage(HttpMethod.Post,
                $"{baseUrl.TrimEnd('/')}/chat/completions")
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Authorization", $"Bearer {apiKey}");

            var resp = await http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("OpenAI/Groq direct error {Status}: {Body}", resp.StatusCode, json);
                // Extract readable error message from API response when available
                string apiError = resp.StatusCode.ToString();
                try
                {
                    using var errDoc = JsonDocument.Parse(json);
                    if (errDoc.RootElement.TryGetProperty("error", out var errEl))
                    {
                        apiError = errEl.TryGetProperty("message", out var msgEl)
                            ? msgEl.GetString() ?? apiError
                            : errEl.GetRawText();
                    }
                }
                catch { /* ignore JSON parse errors on error response */ }
                throw new Exception($"Groq/OpenAI API error ({(int)resp.StatusCode}): {apiError}");
            }

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                       .GetProperty("choices")[0]
                       .GetProperty("message")
                       .GetProperty("content")
                       .GetString()?.Trim() ?? "لم أتمكن من توليد رد.";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  OpenAI GPT — Function Calling (agentic tool-use loop)
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string> CallOpenAIWithToolsAsync(
            string systemPrompt,
            List<(string role, string content)> history,
            string question,
            string apiKey,
            string model,
            int maxTokens,
            string userId,
            string userRole,
            string baseUrl = OpenAIBaseUrl)
        {
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromMinutes(2);

            // OpenAI tools: each tool is wrapped in {"type":"function","function":{...}}
            var toolDefs = BuildAnthropicTools(userRole)
                .Select(t =>
                {
                    var j = JsonNode.Parse(JsonSerializer.Serialize(t))!.AsObject();
                    return new JsonObject
                    {
                        ["type"]     = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"]        = j["name"]!.DeepClone(),
                            ["description"] = j["description"]!.DeepClone(),
                            ["parameters"]  = j["input_schema"]!.DeepClone()
                        }
                    };
                })
                .ToArray();

            // Build messages list (system + history + user question)
            var messages = new JsonArray();
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = systemPrompt });
            foreach (var (r, c) in history)
                messages.Add(new JsonObject { ["role"] = r == "user" ? "user" : "assistant", ["content"] = c });
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = question });

            for (int iter = 0; iter < 5; iter++)
            {
                var body = new JsonObject
                {
                    ["model"]      = model,
                    ["max_tokens"] = maxTokens,
                    ["messages"]   = messages.DeepClone(),
                    ["tools"]      = JsonNode.Parse(JsonSerializer.Serialize(toolDefs))!
                };

                var req = new HttpRequestMessage(HttpMethod.Post,
                    $"{baseUrl.TrimEnd('/')}/chat/completions")
                {
                    Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
                };
                req.Headers.Add("Authorization", $"Bearer {apiKey}");

                var resp = await http.SendAsync(req);
                var json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI/Groq tool-use error {Status}: {Body}", resp.StatusCode, json);
                    string toolApiError = $"{(int)resp.StatusCode}";
                    try
                    {
                        using var errDoc = JsonDocument.Parse(json);
                        if (errDoc.RootElement.TryGetProperty("error", out var errEl))
                            toolApiError = errEl.TryGetProperty("message", out var mEl)
                                ? mEl.GetString() ?? toolApiError : errEl.GetRawText();
                    }
                    catch { }
                    throw new Exception($"Groq/OpenAI tool-use error ({(int)resp.StatusCode}): {toolApiError}");
                }

                using var doc       = JsonDocument.Parse(json);
                var choice          = doc.RootElement.GetProperty("choices")[0];
                var finishReason    = choice.GetProperty("finish_reason").GetString();
                var assistantMsg    = choice.GetProperty("message");
                var assistantRaw    = assistantMsg.GetRawText();

                // Append assistant message to conversation
                messages.Add(JsonNode.Parse(assistantRaw)!.DeepClone());

                // If no tool calls or model finished → return text
                if (finishReason != "tool_calls" ||
                    !assistantMsg.TryGetProperty("tool_calls", out var toolCallsEl))
                {
                    return assistantMsg.TryGetProperty("content", out var contentEl)
                        ? contentEl.GetString()?.Trim() ?? "لم أتمكن من توليد رد."
                        : "لم أتمكن من توليد رد.";
                }

                // Execute tool calls in PARALLEL
                var toolCalls = toolCallsEl.EnumerateArray()
                    .Select(tc => (
                        id:   tc.GetProperty("id").GetString()!,
                        name: tc.GetProperty("function").GetProperty("name").GetString()!,
                        input: tc.GetProperty("function").TryGetProperty("arguments", out var args)
                               ? (JsonElement?)JsonDocument.Parse(args.GetString() ?? "{}").RootElement
                               : null
                    )).ToList();

                _logger.LogDebug("OpenAI tools ({Count}): {Names}", toolCalls.Count, string.Join(", ", toolCalls.Select(t => t.name)));

                var toolPairs2 = new List<(string id, string result)>();
                foreach (var tc in toolCalls)
                {
                    var argEl = tc.input ?? JsonDocument.Parse("{}").RootElement;
                    toolPairs2.Add((tc.id, await ExecuteToolAsync(tc.name, argEl, userId, userRole)));
                }
                var toolPairs = toolPairs2;

                // Append one tool result message per call (OpenAI format: role="tool")
                foreach (var (id, result) in toolPairs)
                    messages.Add(new JsonObject
                    {
                        ["role"]         = "tool",
                        ["tool_call_id"] = id,
                        ["content"]      = result
                    });
            }

            return "عذراً، تعذّر الحصول على إجابة. يرجى المحاولة مجدداً.";
        }

        // ── Tool definitions (role-filtered) ─────────────────────────────────

        private static object[] BuildAnthropicTools(string role)
        {
            var tools = new List<object>();

            if (role is "Admin" or "Receptionist")
            {
                tools.Add(Tool("get_center_statistics",
                    "إحصائيات المركز الشاملة: إجمالي المتدربين (ذكور+إناث)، المدربين، الدورات النشطة، الامتحانات، الشهادات، الجلسات القادمة، إجمالي الإيرادات، مدفوعات اليوم، طلبات دفع معلقة، عدد أسئلة AI اليوم"));
                tools.Add(Tool("get_course_details",
                    "تفاصيل كل دورة نشطة: اسم الدورة، رقم الدفعة، عدد المتدربين المسجلين، عدد المدربين المُعيَّنين وأسماؤهم، السعر والعملة"));
                tools.Add(Tool("get_payment_breakdown",
                    "تحليل المدفوعات: الإجمالي الكلي، عدد الدفعات، مدفوعات اليوم، طلبات قيد المراجعة، التوزيع بالعملات"));
                tools.Add(Tool("get_exam_statistics",
                    "إحصائيات الامتحانات: عدد الامتحانات المُعدَّة، إجمالي المحاولات، عدد الناجحين والراسبين مع النسب المئوية، متوسط الدرجات"));
            }

            if (role == "Trainee")
            {
                tools.Add(Tool("get_my_courses",
                    "الدورات المسجل بها المتدرب: اسم الدورة، رقم الدفعة، السعر والعملة، عدد المحاضرات الكلي، تاريخ البدء، عدد المدربين المُعيَّنين للدورة وأسماؤهم، وصف الدورة"));
                tools.Add(Tool("get_my_attendance",
                    "سجل حضور المتدرب لكل دورة: عدد المحاضرات الحاضرة، عدد الغيابات، النسبة المئوية للحضور، التقييم (ممتاز/مقبول/منخفض)"));
                tools.Add(Tool("get_my_exam_results",
                    "نتائج امتحانات المتدرب: اسم الدورة، اسم الامتحان، الدرجة المحصّلة، النجاح أو الرسوب، تاريخ الامتحان، متوسط درجاته العام"));
                tools.Add(Tool("get_my_payments",
                    "سجل مدفوعات المتدرب: مبلغ كل دفعة، العملة، التاريخ، إجمالي جميع المدفوعات"));
                tools.Add(Tool("get_my_sessions",
                    "الجلسات المباشرة القادمة في دورات المتدرب: عنوان الجلسة، اسم الدورة، الموعد المحدد"));
                tools.Add(Tool("get_my_certificates",
                    "الشهادات التي حصل عليها المتدرب: اسم الدورة، المعدل المئوي"));
            }

            if (role == "Trainer")
            {
                tools.Add(Tool("get_my_teaching_courses",
                    "الدورات التي يُدرِّسها المدرب حالياً: اسم الدورة، عدد المتدربين المسجلين في كل دورة"));
                tools.Add(Tool("get_my_sessions",
                    "الجلسات المباشرة القادمة التي أنشأها المدرب: العنوان، الدورة، الموعد"));
                tools.Add(Tool("get_my_exam_stats",
                    "إحصائيات الامتحانات التي أنشأها المدرب: عدد الامتحانات، إجمالي المحاولات، نسب النجاح والرسوب، متوسط الدرجات"));
            }

            return tools.ToArray();
        }

        private static object Tool(string name, string description) => new
        {
            name,
            description,
            input_schema = new { type = "object", properties = new { } }
        };

        // ── Tool dispatcher ───────────────────────────────────────────────────

        private Task<string> ExecuteToolAsync(string name, JsonElement _, string userId, string userRole)
            => name switch
            {
                "get_center_statistics"    => ToolCenterStatisticsAsync(),
                "get_course_details"       => ToolCourseDetailsAsync(),
                "get_payment_breakdown"    => ToolPaymentBreakdownAsync(),
                "get_exam_statistics"      => ToolExamStatisticsAsync(),
                "get_my_courses"           => ToolMyCoursesAsync(userId),
                "get_my_attendance"        => ToolMyAttendanceAsync(userId),
                "get_my_exam_results"      => ToolMyExamResultsAsync(userId),
                "get_my_payments"          => ToolMyPaymentsAsync(userId),
                "get_my_sessions"          => ToolMySessionsAsync(userId, userRole),
                "get_my_certificates"      => ToolMyCertificatesAsync(userId),
                "get_my_teaching_courses"  => ToolMyTeachingCoursesAsync(userId),
                "get_my_exam_stats"        => ToolMyExamStatsAsync(userId),
                _                          => Task.FromResult($"الأداة '{name}' غير معرّفة.")
            };

        // ── Admin / Receptionist tools ────────────────────────────────────────

        private async Task<string> ToolCenterStatisticsAsync()
        {
            var totalTrainees  = await _context.Trainees.CountAsync();
            var femaleTrainees = await _context.Users.CountAsync(u => u.Role == RoleType.Trainee && u.Gender == Gender.Female);
            var totalTrainers  = await _context.Trainers.CountAsync();
            var femaleTrainers = await _context.Users.CountAsync(u => u.Role == RoleType.Trainer && u.Gender == Gender.Female);
            var courses        = await _context.Courses.CountAsync(c => !c.IsDeleted);
            var exams          = await _context.Exams.CountAsync();
            var revenue        = await _context.Payments.Where(p => !p.IsDeleted).SumAsync(p => p.TotalAmount);
            var certs          = await _context.Certificates.CountAsync();
            var sessions       = await _context.LiveSessions.CountAsync(ls => !ls.IsCancelled && ls.ScheduledAt >= DateTime.UtcNow);
            var todayPayments  = await _context.Payments.CountAsync(p => !p.IsDeleted && p.CreatedDate >= DateTime.UtcNow.Date);
            var pendingReqs    = await _context.PaymentRequests.CountAsync(r => r.Status == PaymentRequestStatus.Pending);
            var aiToday        = await _context.AIChatMessages.CountAsync(m => m.IsAnswered && m.CreatedAt >= DateTime.UtcNow.Date);

            return $"""
                إحصائيات مركز التدريب ({DateTime.Now:dd/MM/yyyy}):
                المتدربون: {totalTrainees} ({totalTrainees - femaleTrainees} ذكور، {femaleTrainees} إناث)
                المدربون: {totalTrainers} ({totalTrainers - femaleTrainers} ذكور، {femaleTrainers} إناث)
                الدورات النشطة: {courses}
                الامتحانات: {exams}
                الشهادات الصادرة: {certs}
                الجلسات القادمة: {sessions}
                إجمالي الإيرادات: {revenue:N0}
                مدفوعات اليوم: {todayPayments}
                طلبات دفع معلقة: {pendingReqs}
                أسئلة AI اليوم: {aiToday}
                """;
        }

        private async Task<string> ToolCourseDetailsAsync()
        {
            var courses = await _context.Courses
                .Where(c => !c.IsDeleted)
                .Include(c => c.CourseTrainers)
                    .ThenInclude(ct => ct.Trainer)
                        .ThenInclude(t => t.User)
                .Include(c => c.CourseTrainees)
                .AsNoTracking()
                .OrderByDescending(c => c.CourseTrainees.Count)
                .ToListAsync();

            if (!courses.Any()) return "لا توجد دورات نشطة.";

            var sb = new StringBuilder($"الدورات النشطة ({courses.Count} دورة):\n");
            foreach (var c in courses)
            {
                var trainers = c.CourseTrainers
                    .Select(ct => ct.Trainer?.User?.FullName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
                sb.AppendLine($"- {c.CourseName} (دفعة {c.BatchNumber}): {c.CourseTrainees.Count} متدرب | {trainers.Count} مدرب | السعر: {c.Price:N0} {c.CourseCurrency}");
                if (trainers.Count > 0)
                    sb.AppendLine($"  المدربون: {string.Join("، ", trainers)}");
            }
            return sb.ToString();
        }

        private async Task<string> ToolPaymentBreakdownAsync()
        {
            var pmts    = await _context.Payments.Where(p => !p.IsDeleted).ToListAsync();
            var pending = await _context.PaymentRequests.CountAsync(r => r.Status == PaymentRequestStatus.Pending);
            if (!pmts.Any()) return "لا توجد سجلات دفع.";

            var sb = new StringBuilder("تفاصيل المدفوعات:\n");
            sb.AppendLine($"إجمالي الإيرادات: {pmts.Sum(p => p.TotalAmount):N0}");
            sb.AppendLine($"إجمالي الدفعات: {pmts.Count}");
            sb.AppendLine($"مدفوعات اليوم: {pmts.Count(p => p.CreatedDate >= DateTime.UtcNow.Date)}");
            sb.AppendLine($"طلبات دفع قيد المراجعة: {pending}");
            sb.AppendLine("التوزيع بالعملة:");
            foreach (var g in pmts.GroupBy(p => p.Currency))
                sb.AppendLine($"  - {g.Key}: {g.Sum(p => p.TotalAmount):N0} ({g.Count()} دفعة)");
            return sb.ToString();
        }

        private async Task<string> ToolExamStatisticsAsync()
        {
            var total   = await _context.ExamAttempts.CountAsync(a => a.SubmittedAt != null);
            var passed  = await _context.ExamAttempts.CountAsync(a => a.SubmittedAt != null && a.IsPassed == true);
            var exams   = await _context.Exams.CountAsync();
            var avgScore = total > 0
                ? await _context.ExamAttempts.Where(a => a.SubmittedAt != null).AverageAsync(a => a.TotalScore)
                : 0;

            return $"""
                إحصائيات الامتحانات:
                الامتحانات المُعدّة: {exams}
                محاولات الاختبار: {total}
                الناجحون: {passed} ({(total > 0 ? passed * 100.0 / total : 0):F1}%)
                الراسبون: {total - passed} ({(total > 0 ? (total - passed) * 100.0 / total : 0):F1}%)
                متوسط الدرجات: {avgScore:F1}
                """;
        }

        // ── Trainee tools ─────────────────────────────────────────────────────

        private async Task<string> ToolMyCoursesAsync(string userId)
        {
            var trainee = await _context.Trainees
                .Include(t => t.CourseTrainees)
                    .ThenInclude(ct => ct.Course)
                        .ThenInclude(c => c.CourseTrainers)
                            .ThenInclude(ctr => ctr.Trainer)
                                .ThenInclude(tr => tr.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return "لم يُعثر على سجل متدرب.";

            var courses = trainee.CourseTrainees.Where(ct => ct.Course != null && !ct.Course.IsDeleted).ToList();
            if (!courses.Any()) return "غير مسجل في أي دورة حالياً.";

            var sb = new StringBuilder($"الدورات المسجلة ({courses.Count} دورة):\n");
            foreach (var ct in courses)
            {
                var c = ct.Course;
                var trainers = c.CourseTrainers?
                    .Select(ctr => ctr.Trainer?.User?.FullName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList() ?? new List<string?>();

                sb.AppendLine($"- {c.CourseName} (دفعة {c.BatchNumber}):");
                sb.AppendLine($"  السعر: {c.Price:N0} {c.CourseCurrency} | عدد المحاضرات: {c.NumberOfLectures} | تاريخ البدء: {c.ReleaseDate:dd/MM/yyyy}");
                sb.AppendLine($"  عدد المدربين: {trainers.Count}");
                if (trainers.Count > 0)
                    sb.AppendLine($"  أسماء المدربين: {string.Join("، ", trainers)}");
                if (!string.IsNullOrWhiteSpace(c.Description))
                    sb.AppendLine($"  الوصف: {c.Description}");
            }
            return sb.ToString();
        }

        private async Task<string> ToolMyAttendanceAsync(string userId)
        {
            var trainee = await _context.Trainees
                .Include(t => t.CourseTrainees).ThenInclude(ct => ct.Course)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return "لم يُعثر على سجل متدرب.";

            var courses = trainee.CourseTrainees.Where(ct => ct.Course != null && !ct.Course.IsDeleted).ToList();
            if (!courses.Any()) return "غير مسجل في أي دورة.";

            var sb = new StringBuilder("سجل الحضور:\n");
            foreach (var ct in courses)
            {
                var total    = await _context.Lectures.CountAsync(l => l.CourseId == ct.CourseId && !l.IsDeleted);
                var attended = await _context.Presences.CountAsync(p =>
                    p.TraineeId == trainee.TraineeId && p.Lecture.CourseId == ct.CourseId && p.IsPresent);
                var absent   = total - attended;
                var pct      = total > 0 ? attended * 100.0 / total : 0;
                var status   = pct >= 75 ? "ممتاز ✓" : pct >= 60 ? "مقبول" : "منخفض ⚠ — قد يؤثر على شهادتك";
                sb.AppendLine($"- {ct.Course.CourseName}:");
                sb.AppendLine($"  حضر {attended} | غائب {absent} | من إجمالي {total} محاضرة → النسبة: {pct:F1}% ({status})");
            }
            return sb.ToString();
        }

        private async Task<string> ToolMyExamResultsAsync(string userId)
        {
            var trainee = await _context.Trainees.AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return "لم يُعثر على سجل متدرب.";

            var attempts = await _context.ExamAttempts
                .Include(a => a.Exam).ThenInclude(e => e.Course)
                .Where(a => a.TraineeId == trainee.TraineeId && a.SubmittedAt != null)
                .OrderByDescending(a => a.SubmittedAt)
                .Take(20).AsNoTracking().ToListAsync();

            if (!attempts.Any()) return "لم تُؤدِّ أي امتحانات حتى الآن.";

            var passed = attempts.Count(a => a.IsPassed == true);
            var avg    = attempts.Average(a => a.TotalScore);
            var sb = new StringBuilder($"نتائج الامتحانات ({attempts.Count} امتحان | {passed} ناجح، {attempts.Count - passed} راسب | متوسط الدرجات: {avg:F1}):\n");
            foreach (var a in attempts)
            {
                var courseName = a.Exam?.Course?.CourseName ?? "—";
                sb.AppendLine($"- [{courseName}] {a.Exam?.ExamName}: {a.TotalScore:F1} نقطة — {(a.IsPassed == true ? "ناجح ✓" : "راسب ✗")} ({a.SubmittedAt!.Value:dd/MM/yyyy})");
            }
            return sb.ToString();
        }

        private async Task<string> ToolMyPaymentsAsync(string userId)
        {
            var trainee = await _context.Trainees
                .Include(t => t.Payments.Where(p => !p.IsDeleted))
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return "لم يُعثر على سجل متدرب.";

            var pmts = trainee.Payments.OrderByDescending(p => p.CreatedDate).ToList();
            if (!pmts.Any()) return "لا توجد سجلات دفع.";

            var sb = new StringBuilder($"سجل المدفوعات ({pmts.Count} دفعة):\n");
            foreach (var p in pmts)
                sb.AppendLine($"- {p.TotalAmount:N0} {p.Currency} | {p.CreatedDate:dd/MM/yyyy}");
            sb.AppendLine($"الإجمالي المدفوع: {pmts.Sum(p => p.TotalAmount):N0}");
            return sb.ToString();
        }

        private async Task<string> ToolMySessionsAsync(string userId, string role)
        {
            List<LiveSession> sessions;
            if (role == "Trainer")
            {
                sessions = await _context.LiveSessions
                    .Include(ls => ls.Course)
                    .Where(ls => ls.CreatedByUserId == userId && !ls.IsCancelled && ls.ScheduledAt >= DateTime.UtcNow)
                    .OrderBy(ls => ls.ScheduledAt).Take(10).AsNoTracking().ToListAsync();
            }
            else
            {
                var trainee = await _context.Trainees.Include(t => t.CourseTrainees).AsNoTracking()
                    .FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainee == null) return "لم يُعثر على سجل متدرب.";
                var ids = trainee.CourseTrainees.Select(ct => ct.CourseId).ToList();
                sessions = await _context.LiveSessions
                    .Include(ls => ls.Course)
                    .Where(ls => ids.Contains(ls.CourseId) && !ls.IsCancelled && ls.ScheduledAt >= DateTime.UtcNow.AddMinutes(-60))
                    .OrderBy(ls => ls.ScheduledAt).Take(10).AsNoTracking().ToListAsync();
            }

            if (!sessions.Any()) return "لا توجد جلسات مباشرة قادمة.";

            var sb = new StringBuilder($"الجلسات القادمة ({sessions.Count}):\n");
            foreach (var ls in sessions)
                sb.AppendLine($"- {ls.Title} ({ls.Course?.CourseName ?? "—"}) | {ls.ScheduledAt.ToLocalTime():dd/MM/yyyy hh:mm tt}");
            return sb.ToString();
        }

        private async Task<string> ToolMyCertificatesAsync(string userId)
        {
            var trainee = await _context.Trainees
                .Include(t => t.Certificates).ThenInclude(c => c.Course)
                .AsNoTracking().FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return "لم يُعثر على سجل متدرب.";

            var certs = trainee.Certificates.Where(c => !c.IsDeleted).ToList();
            if (!certs.Any()) return "لم تحصل على أي شهادات حتى الآن.";

            var sb = new StringBuilder($"الشهادات الحاصل عليها ({certs.Count} شهادة):\n");
            foreach (var c in certs)
                sb.AppendLine($"- {c.Course?.CourseName ?? "—"} | المعدل: {c.Average:F1}%");
            return sb.ToString();
        }

        // ── Trainer tools ─────────────────────────────────────────────────────

        private async Task<string> ToolMyTeachingCoursesAsync(string userId)
        {
            var trainer = await _context.Trainers
                .Include(t => t.CourseTrainers).ThenInclude(ct => ct.Course)
                .AsNoTracking().FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainer == null) return "لم يُعثر على سجل مدرب.";
            if (!trainer.CourseTrainers.Any()) return "لا توجد دورات مسندة إليك.";

            var sb = new StringBuilder($"الدورات التي تُدرِّسها ({trainer.CourseTrainers.Count} دورة):\n");
            foreach (var ct in trainer.CourseTrainers)
            {
                var cnt = await _context.CourseTrainees.CountAsync(c => c.CourseId == ct.CourseId);
                sb.AppendLine($"- {ct.Course.CourseName} | {cnt} متدرب");
            }
            return sb.ToString();
        }

        private async Task<string> ToolMyExamStatsAsync(string userId)
        {
            var total   = await _context.Exams.CountAsync(e => e.Trainer != null && e.Trainer.UserId == userId);
            var attempts = await _context.ExamAttempts
                .Include(a => a.Exam).ThenInclude(e => e.Trainer)
                .Where(a => a.Exam.Trainer != null && a.Exam.Trainer.UserId == userId && a.SubmittedAt != null)
                .ToListAsync();
            var passed = attempts.Count(a => a.IsPassed == true);

            return $"""
                إحصائيات امتحاناتك:
                الامتحانات التي أنشأتها: {total}
                إجمالي المحاولات: {attempts.Count}
                الناجحون: {passed} ({(attempts.Any() ? passed * 100.0 / attempts.Count : 0):F1}%)
                الراسبون: {attempts.Count - passed}
                متوسط الدرجات: {(attempts.Any() ? attempts.Average(a => a.TotalScore) : 0):F1}
                """;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Claude API — raw HTTP call (legacy, used only from Ollama fallback path)
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string> CallAnthropicAsync(
            string systemPrompt,
            List<(string role, string content)> history,
            string question,
            string apiKey,
            int maxTokens      = 1024,
            int timeoutMinutes = 2)
        {
            var messages = new List<object>();
            foreach (var (r, c) in history)
                messages.Add(new { role = r, content = c });
            messages.Add(new { role = "user", content = question });

            var body = JsonSerializer.Serialize(new
            {
                model      = AnthropicModel,
                max_tokens = maxTokens,
                system     = systemPrompt,
                messages
            });

            var http    = _httpFactory.CreateClient();
            http.Timeout = timeoutMinutes <= 0
                ? Timeout.InfiniteTimeSpan
                : TimeSpan.FromMinutes(timeoutMinutes);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key",         apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            var response = await http.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic API error {Status}: {Body}", response.StatusCode, json);
                throw new Exception($"Anthropic API error: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                      .GetProperty("content")[0]
                      .GetProperty("text")
                      .GetString() ?? "لم أتمكن من توليد رد.";
        }

        /// <param name="timeoutMinutes">
        /// HTTP timeout in minutes.
        /// Chat calls use 3 min; lecture-analysis calls use 10 min
        /// because the model must generate thousands of tokens.
        /// </param>
        /// <param name="numCtx">
        /// Context window size (tokens).  8 192 is fine for chat.
        /// Lecture prompts are much larger — pass 16 384 for those.
        /// </param>
        /// <param name="numPredict">
        /// Maximum tokens to generate (-1 = unlimited).
        /// Pass minTokens for lecture calls so Ollama knows the target length.
        /// </param>
        private async Task<string> CallOllamaAsync(
            string systemPrompt,
            List<(string role, string content)> history,
            string question,
            string ollamaUrl,
            string ollamaModel,
            int timeoutMinutes = 3,
            int numCtx         = 8192,
            int numPredict     = -1)
        {
            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };
            foreach (var (r, c) in history)
                messages.Add(new { role = r, content = c });
            messages.Add(new { role = "user", content = question });

            var body = JsonSerializer.Serialize(new
            {
                model    = ollamaModel,
                messages,
                stream   = false,
                options  = new
                {
                    temperature = 0.3,       // more deterministic → fewer hallucinations
                    num_ctx     = numCtx,    // caller controls context window
                    top_p       = 0.9,
                    num_predict = numPredict // -1 = unlimited; lecture calls pass minTokens
                }
            });

            var http     = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
            var request  = new HttpRequestMessage(
                HttpMethod.Post, $"{ollamaUrl.TrimEnd('/')}/api/chat")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            var response = await http.SendAsync(request);
            var json     = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Ollama error {Status}: {Body}", response.StatusCode, json);
                throw new Exception($"Ollama API error: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString() ?? "لم أتمكن من توليد رد.";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Ollama — Pseudo-Function-Calling (guided tool use)
        //
        //  Ollama models don't support JSON function calling natively.
        //  We simulate it by:
        //    1. Appending a plain-text tool listing to the system prompt.
        //    2. First pass: Ollama may respond with "FETCH: <tool_name>" lines.
        //    3. We execute those tools against the real DB (in parallel).
        //    4. Second pass: Ollama answers using the fetched data.
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string> CallOllamaWithGuidedToolsAsync(
            string systemPrompt,
            List<(string role, string content)> history,
            string question,
            string ollamaUrl,
            string ollamaModel,
            string userId,
            string userRole)
        {
            // Build a human-readable tool menu appended to the system prompt
            var toolsJson = JsonSerializer.Serialize(BuildAnthropicTools(userRole));
            using var toolsDoc = JsonDocument.Parse(toolsJson);

            var toolMenu = new StringBuilder();
            toolMenu.AppendLine("\n\n---");
            toolMenu.AppendLine("## أدوات استعلام قاعدة البيانات");
            toolMenu.AppendLine("إذا احتجت بيانات محددة لتحسين ردك، اكتب في بداية ردك:");
            toolMenu.AppendLine("FETCH: <اسم_الأداة>");
            toolMenu.AppendLine("يمكنك طلب أكثر من أداة (سطر لكل أداة).");
            toolMenu.AppendLine("إذا البيانات الموجودة كافية أو السؤال لا يحتاج بيانات، أجب مباشرة.\n");
            toolMenu.AppendLine("الأدوات المتاحة:");

            foreach (var tool in toolsDoc.RootElement.EnumerateArray())
            {
                var name = tool.GetProperty("name").GetString();
                var desc = tool.GetProperty("description").GetString();
                toolMenu.AppendLine($"  • {name} — {desc}");
            }
            toolMenu.AppendLine("---");

            var guidedPrompt = systemPrompt + toolMenu;

            // ── First pass ───────────────────────────────────────────────────
            var firstPass = await CallOllamaAsync(guidedPrompt, history, question, ollamaUrl, ollamaModel);

            // Parse FETCH: requests (case-insensitive, leading whitespace tolerant)
            var fetchRequests = firstPass
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.StartsWith("FETCH:", StringComparison.OrdinalIgnoreCase))
                .Select(l => l[6..].Trim())   // strip "FETCH:"
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();

            // No tool requests → first pass IS the final answer
            if (!fetchRequests.Any())
                return SanitizeLlmResponse(firstPass);

            _logger.LogDebug("Ollama FETCH ({Count}): {Names}",
                fetchRequests.Count, string.Join(", ", fetchRequests));

            // ── Execute all fetches in PARALLEL ──────────────────────────────
            var defaultEl   = JsonDocument.Parse("{}").RootElement;
            var fetchTasks  = fetchRequests.Select(name =>
                ExecuteToolAsync(name, defaultEl, userId, userRole));
            var fetchResults = await Task.WhenAll(fetchTasks);

            // ── Build data block & do second pass ────────────────────────────
            var dataBlock = new StringBuilder("\n\n---\n## بيانات قاعدة البيانات المسترجعة:\n");
            for (int i = 0; i < fetchRequests.Count; i++)
            {
                dataBlock.AppendLine($"\n### [{fetchRequests[i]}]");
                dataBlock.AppendLine(fetchResults[i]);
            }
            dataBlock.AppendLine("\n---");
            dataBlock.AppendLine("استناداً إلى البيانات أعلاه، أجب الآن على سؤال المستخدم:");

            var enrichedForFinal = guidedPrompt + dataBlock;
            var finalPass = await CallOllamaAsync(enrichedForFinal, history, question, ollamaUrl, ollamaModel);

            return SanitizeLlmResponse(finalPass);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core System Prompt
        // ─────────────────────────────────────────────────────────────────────

        private static string BuildSchemaGuide(string role) => role switch
        {
            "Trainee" => """

                ## الأدوات المتاحة — استدعِ الأداة المناسبة عند الحاجة
                • get_my_courses        ← دوراتك المسجلة: الاسم، المدربون وأسماؤهم، السعر، عدد المحاضرات، تاريخ البدء
                • get_my_attendance     ← نسبة حضورك وعدد المحاضرات الحاضرة/الغائبة لكل دورة
                • get_my_exam_results   ← درجات امتحاناتك: الدورة، اسم الامتحان، الدرجة، النجاح/الرسوب، التاريخ
                • get_my_payments       ← مدفوعاتك: المبالغ، التواريخ، الإجمالي
                • get_my_sessions       ← جلساتك المباشرة القادمة: العنوان، الدورة، الموعد
                • get_my_certificates   ← شهاداتك: الدورة، المعدل

                قاعدة: أي سؤال عن بيانات → استدعِ الأداة أولاً → أجب من النتيجة.
                """,
            "Admin" or "Receptionist" => """

                ## الأدوات المتاحة — استدعِ الأداة المناسبة عند الحاجة
                • get_center_statistics ← إحصائيات المركز: عدد المتدربين (ذكور/إناث)، المدربون، الدورات، الإيرادات، الامتحانات، الشهادات، الجلسات
                • get_course_details    ← قائمة الدورات: الاسم، عدد المتدربين، أسماء المدربين، السعر
                • get_payment_breakdown ← تفاصيل المدفوعات: الإجمالي، التوزيع بالعملة، طلبات معلقة
                • get_exam_statistics   ← إحصائيات الامتحانات: نسب النجاح، متوسط الدرجات، عدد المحاولات

                قاعدة: أي سؤال عن بيانات → استدعِ الأداة أولاً → أجب من النتيجة.
                """,
            "Trainer" => """

                ## الأدوات المتاحة — استدعِ الأداة المناسبة عند الحاجة
                • get_my_teaching_courses ← دوراتك: الاسم، عدد المتدربين المسجلين
                • get_my_sessions         ← جلساتك المباشرة القادمة: العنوان، الدورة، الموعد
                • get_my_exam_stats       ← إحصائيات امتحاناتك: نسب النجاح، متوسط الدرجات

                قاعدة: أي سؤال عن بيانات → استدعِ الأداة أولاً → أجب من النتيجة.
                """,
            _ => ""
        };

        private static string BuildCoreSystemPrompt(ApplicationUser user, string role)
        {
            var today  = DateTime.Now.ToString("dd/MM/yyyy");
            var roleAr = RoleArabic(role);

            return $"""
                أنت "مساعد المركز" — المساعد الذكي الرسمي لنظام إدارة مركز التدريب.
                تاريخ اليوم: {today}

                ## المستخدم الحالي
                الاسم: {user.FullName} | الدور: {roleAr}

                ## ما تستطيع مساعدتك فيه ({roleAr})
                {GetRoleCapabilities(role)}

                ## أسلوبك الإلزامي
                - تحدث دائماً بالعربية بأسلوب ودي ومختصر ومهني
                - استخدم القوائم (•) لعرض البيانات المتعددة
                - استخدم التنسيق (الأرقام، النسب المئوية) بدقة
                - اجعل ردودك عملية ومفيدة، وليست مجرد معلومات جافة

                ## قواعد صارمة
                - عند الحاجة لبيانات (دورات، امتحانات، مدفوعات، إحصائيات...) استدعِ الأداة المناسبة أولاً، ثم أجب بناءً على نتائجها
                - لا تخترع أرقاماً أو بيانات — كل رقم يجب أن يأتي من أداة استُدعيت فعلاً
                - لا تشارك بيانات مستخدم مع مستخدم آخر
                - إذا لم تجد أداة تُجيب على السؤال، قل: "هذه المعلومة غير متاحة حالياً، تواصل مع الإدارة"
                - لا تجب على أسئلة خارج نطاق مركز التدريب

                ## أمثلة على ردودك المثالية

                المستخدم: "ما دوراتي؟"
                [بيانات النظام]
                الدورات المسجلة (2 دورة):
                  - Python للمبتدئين (دفعة 3) | السعر: 150,000 ل.س
                  - تصميم الويب (دفعة 1) | السعر: 120,000 ل.س
                [/بيانات النظام]
                المساعد: "أنت مسجل حالياً في دورتين:
                • Python للمبتدئين — دفعة 3 (150,000 ل.س)
                • تصميم الويب — دفعة 1 (120,000 ل.س)
                هل تريد تفاصيل عن إحداهما؟"

                المستخدم: "كيف أسجل في دورة جديدة؟"
                المساعد: "للتسجيل في دورة جديدة، تواصل مع موظف الاستقبال في المركز أو راجع الدورات المتاحة عبر الموقع. سيساعدك في اختيار الدورة المناسبة وإتمام إجراءات التسجيل."

                المستخدم: "هل أنا بمستوى جيد في امتحاناتي؟"
                [بيانات النظام]
                نتائج الامتحانات (3 امتحانات):
                  - امتحان Python 1: 85 نقطة — ناجح ✓
                  - امتحان HTML: 60 نقطة — ناجح ✓
                  - امتحان CSS: 45 نقطة — راسب ✗
                الملخص: 2 ناجح، 1 راسب
                [/بيانات النظام]
                المساعد: "نتائجك جيدة بشكل عام! اجتزت 2 من 3 امتحانات:
                • Python و HTML: ممتاز، خاصة Python بـ 85 نقطة
                • CSS: تحتاج للمراجعة، الدرجة 45 لم تكن كافية
                أنصحك بمراجعة محاضرات CSS والاستفسار من المدرب عن النقاط الضعيفة."
                """;
        }

        private static string GetRoleCapabilities(string role) => role switch
        {
            "Trainee" => """
                - دوراتك المسجلة وتفاصيلها
                - سجل حضورك ونسبه في كل دورة
                - نتائج امتحاناتك (ناجح/راسب والدرجات)
                - سجل مدفوعاتك
                - الجلسات المباشرة القادمة لدوراتك
                - شهاداتك الحاصل عليها
                - الإجابة على الأسئلة العامة عن المركز وإجراءاته
                """,
            "Trainer" => """
                - الدورات المُسندة إليك وعدد المتدربين في كل منها
                - جلساتك المباشرة المجدولة القادمة
                - إحصائيات الامتحانات التي أنشأتها
                - الأسئلة المتعلقة بإدارة الدورات والتدريس
                """,
            "Admin" => """
                - إحصائيات شاملة للنظام (متدربون، مدربون، دورات، إيرادات)
                - مراقبة أداء المركز وإيراداته
                - جميع الأسئلة الإدارية والتشغيلية
                """,
            "Receptionist" => """
                - ملخص يومي: المتدربون، الدورات، المدفوعات المسجلة اليوم
                - أسئلة متعلقة بالتسجيل والاستقبال
                """,
            _ => "- الأسئلة العامة عن مركز التدريب"
        };

        // ─────────────────────────────────────────────────────────────────────
        //  Contextual User Message Builder  (RAG — data injected into message)
        //  Called for Layer-2 (general/conversational) questions.
        //  Injects KB entries + role-specific user data so the LLM can give
        //  personalised answers even for open-ended questions.
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string> BuildContextualUserMessageAsync(
            string userId, string role, ApplicationUser user, string question)
        {
            var sb = new StringBuilder();

            // ── Knowledge base entries ────────────────────────────────────────
            var kb = await _context.AIKnowledgeEntries
                .Where(e => e.IsActive)
                .OrderBy(e => e.SortOrder).ThenBy(e => e.CreatedAt)
                .AsNoTracking().Take(8)
                .ToListAsync();

            if (kb.Any())
            {
                sb.AppendLine("[معلومات المركز العامة]");
                foreach (var g in kb.GroupBy(k => k.Category))
                {
                    sb.AppendLine($"[{g.Key}]");
                    foreach (var k in g)
                        sb.AppendLine($"- {k.Title}: {k.Content}");
                }
                sb.AppendLine("[/معلومات المركز العامة]");
                sb.AppendLine();
            }

            // ── Keyword detection for smart data injection ────────────────────
            var q = question;
            bool isCourseQ  = q.Contains("دور")   || q.Contains("كورس")  || q.Contains("مقرر") || q.Contains("course",      StringComparison.OrdinalIgnoreCase);
            bool isAttendQ  = q.Contains("حضو")   || q.Contains("غياب")  || q.Contains("attend", StringComparison.OrdinalIgnoreCase);
            bool isExamQ    = q.Contains("امتحا") || q.Contains("نتيج")  || q.Contains("درجة") || q.Contains("علامة") || q.Contains("ناجح") || q.Contains("راسب") || q.Contains("اختبا");
            bool isPayQ     = q.Contains("دفع")   || q.Contains("فاتور") || q.Contains("مبلغ") || q.Contains("رسوم") || q.Contains("ديون") || q.Contains("متبق");
            bool isSessionQ = q.Contains("جلسة")  || q.Contains("مباشر") || q.Contains("لايف") || q.Contains("موعد") || q.Contains("session", StringComparison.OrdinalIgnoreCase);
            bool isCertQ    = q.Contains("شهادة") || q.Contains("شهادا") || q.Contains("certificate", StringComparison.OrdinalIgnoreCase);
            bool isStatsQ   = q.Contains("إحصا")  || q.Contains("تقرير") || q.Contains("إيراد") || q.Contains("مجموع");
            bool isAdviceQ  = q.Contains("أنصح")  || q.Contains("انصح")  || q.Contains("اقترح") || q.Contains("مستوا") || q.Contains("تقييم") || q.Contains("أفضل");

            // For advice/recommendation questions or any data-relevant question,
            // inject real user data so the LLM can give personalised answers.
            bool needsData = isCourseQ || isAttendQ || isExamQ || isPayQ
                           || isSessionQ || isCertQ || isStatsQ || isAdviceQ;

            if (needsData)
            {
                var dataBlock = new StringBuilder();

                switch (role)
                {
                    case "Trainee":
                        await InjectTraineeDataAsync(dataBlock, userId,
                            courses:  isCourseQ || isAdviceQ,
                            attend:   isAttendQ || isAdviceQ,
                            exams:    isExamQ   || isAdviceQ,
                            payments: isPayQ,
                            sessions: isSessionQ,
                            certs:    isCertQ   || isAdviceQ);
                        break;

                    case "Trainer":
                        await InjectTrainerDataAsync(dataBlock, userId,
                            includeCourses:  isCourseQ || isStatsQ || isAdviceQ,
                            includeSessions: isSessionQ || isAdviceQ);
                        break;

                    case "Admin":
                        if (isStatsQ || isCourseQ || isPayQ || isAdviceQ)
                            await InjectAdminSummaryAsync(dataBlock);
                        break;

                    case "Receptionist":
                        await InjectReceptionistSummaryAsync(dataBlock);
                        break;
                }

                if (dataBlock.Length > 0)
                {
                    sb.AppendLine("[بيانات النظام]");
                    sb.Append(dataBlock);
                    sb.AppendLine("[/بيانات النظام]");
                    sb.AppendLine();
                }
            }

            sb.AppendLine($"السؤال: {question}");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Data injection helpers
        // ─────────────────────────────────────────────────────────────────────

        private async Task InjectTraineeDataAsync(StringBuilder sb, string userId,
            bool courses, bool attend, bool exams, bool payments, bool sessions, bool certs)
        {
            var trainee = await _context.Trainees
                .Include(t => t.CourseTrainees).ThenInclude(ct => ct.Course)
                .Include(t => t.Payments.Where(p => !p.IsDeleted))
                .Include(t => t.Certificates)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (trainee == null)
            {
                sb.AppendLine("ملاحظة: لم يُعثر على سجل متدرب مرتبط بهذا الحساب.");
                return;
            }

            var enrolledCourses = trainee.CourseTrainees
                .Where(ct => ct.Course != null && !ct.Course.IsDeleted)
                .ToList();

            if (courses || attend || sessions)
            {
                sb.AppendLine($"الدورات المسجلة ({enrolledCourses.Count} دورة):");
                if (!enrolledCourses.Any())
                    sb.AppendLine("  لا توجد دورات مسجلة.");
                else
                    foreach (var ct in enrolledCourses)
                        sb.AppendLine($"  - {ct.Course.CourseName} (دفعة {ct.Course.BatchNumber}) | السعر: {ct.Course.Price:N0} {ct.Course.CourseCurrency}");
                sb.AppendLine();
            }

            if (attend && enrolledCourses.Any())
            {
                sb.AppendLine("نسب الحضور:");
                foreach (var ct in enrolledCourses)
                {
                    var total    = await _context.Lectures.CountAsync(l => l.CourseId == ct.CourseId && !l.IsDeleted);
                    var attended = await _context.Presences.CountAsync(p =>
                        p.TraineeId == trainee.TraineeId && p.Lecture.CourseId == ct.CourseId && p.IsPresent);
                    var pct = total > 0 ? attended * 100.0 / total : 0;
                    sb.AppendLine($"  - {ct.Course.CourseName}: {attended}/{total} محاضرة ({pct:F1}%)");
                }
                sb.AppendLine();
            }

            if (exams)
            {
                var attempts = await _context.ExamAttempts
                    .Include(a => a.Exam)
                    .Where(a => a.TraineeId == trainee.TraineeId && a.SubmittedAt != null)
                    .OrderByDescending(a => a.SubmittedAt)
                    .Take(10)
                    .AsNoTracking()
                    .ToListAsync();

                sb.AppendLine($"نتائج الامتحانات ({attempts.Count} امتحان):");
                if (!attempts.Any())
                    sb.AppendLine("  لم يُؤدَّ أي امتحان حتى الآن.");
                else
                    foreach (var a in attempts)
                        sb.AppendLine($"  - {a.Exam.ExamName}: {a.TotalScore:F1} نقطة — {(a.IsPassed == true ? "ناجح ✓" : "راسب ✗")}");
                sb.AppendLine();
            }

            if (payments)
            {
                var pmts = trainee.Payments.ToList();
                sb.AppendLine($"المدفوعات ({pmts.Count} دفعة):");
                if (!pmts.Any())
                    sb.AppendLine("  لا توجد سجلات دفع.");
                else
                {
                    sb.AppendLine($"  - إجمالي المدفوع: {pmts.Sum(p => p.TotalAmount):N0}");
                    var last = pmts.OrderByDescending(p => p.CreatedDate).First();
                    sb.AppendLine($"  - آخر دفعة: {last.TotalAmount:N0} بتاريخ {last.CreatedDate:dd/MM/yyyy}");
                }
                sb.AppendLine();
            }

            if (certs)
            {
                sb.AppendLine($"الشهادات الحاصل عليها: {trainee.Certificates.Count}");
                sb.AppendLine();
            }

            if (sessions)
            {
                var courseIds  = enrolledCourses.Select(ct => ct.CourseId).ToList();
                var upcomingSessions = await _context.LiveSessions
                    .Include(ls => ls.Course)
                    .Where(ls => courseIds.Contains(ls.CourseId)
                              && !ls.IsCancelled
                              && ls.ScheduledAt >= DateTime.UtcNow.AddMinutes(-60))
                    .OrderBy(ls => ls.ScheduledAt)
                    .Take(5)
                    .AsNoTracking()
                    .ToListAsync();

                sb.AppendLine("الجلسات المباشرة القادمة:");
                if (!upcomingSessions.Any())
                    sb.AppendLine("  لا توجد جلسات قادمة.");
                else
                    foreach (var ls in upcomingSessions)
                        sb.AppendLine($"  - {ls.Title} ({ls.Course.CourseName}) — {ls.ScheduledAt.ToLocalTime():dd/MM/yyyy hh:mm tt}");
                sb.AppendLine();
            }
        }

        private async Task InjectTrainerDataAsync(StringBuilder sb, string userId,
            bool includeCourses, bool includeSessions)
        {
            var trainer = await _context.Trainers
                .Include(t => t.CourseTrainers).ThenInclude(ct => ct.Course)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (trainer == null) { sb.AppendLine("ملاحظة: لم يُعثر على سجل مدرب."); return; }

            if (includeCourses)
            {
                sb.AppendLine($"الدورات التي تُدرِّسها ({trainer.CourseTrainers.Count} دورة):");
                if (!trainer.CourseTrainers.Any())
                    sb.AppendLine("  لا توجد دورات مسندة حالياً.");
                else
                    foreach (var ct in trainer.CourseTrainers)
                    {
                        var cnt = await _context.CourseTrainees.CountAsync(c => c.CourseId == ct.CourseId);
                        sb.AppendLine($"  - {ct.Course.CourseName} — {cnt} متدرب");
                    }
                sb.AppendLine();

                var examCount = await _context.Exams
                    .Where(e => e.Trainer != null && e.Trainer.UserId == userId)
                    .CountAsync();
                sb.AppendLine($"الامتحانات التي أنشأتها: {examCount}");
                sb.AppendLine();
            }

            if (includeSessions)
            {
                var sessions = await _context.LiveSessions
                    .Include(ls => ls.Course)
                    .Where(ls => ls.CreatedByUserId == userId && !ls.IsCancelled
                              && ls.ScheduledAt >= DateTime.UtcNow)
                    .OrderBy(ls => ls.ScheduledAt)
                    .Take(5)
                    .AsNoTracking()
                    .ToListAsync();

                sb.AppendLine("جلساتك المباشرة القادمة:");
                if (!sessions.Any())
                    sb.AppendLine("  لا توجد جلسات مجدولة.");
                else
                    foreach (var ls in sessions)
                        sb.AppendLine($"  - {ls.Title} ({ls.Course.CourseName}) — {ls.ScheduledAt.ToLocalTime():dd/MM/yyyy hh:mm tt}");
                sb.AppendLine();
            }
        }

        private async Task InjectAdminSummaryAsync(StringBuilder sb)
        {
            sb.AppendLine("إحصائيات النظام الحالية:");
            sb.AppendLine($"  - المتدربون: {await _context.Trainees.CountAsync()}");
            sb.AppendLine($"  - المدربون: {await _context.Trainers.CountAsync()}");
            sb.AppendLine($"  - الدورات النشطة: {await _context.Courses.CountAsync(c => !c.IsDeleted)}");
            sb.AppendLine($"  - الامتحانات: {await _context.Exams.CountAsync()}");
            sb.AppendLine($"  - إجمالي الإيرادات: {await _context.Payments.Where(p => !p.IsDeleted).SumAsync(p => p.TotalAmount):N0}");
            sb.AppendLine($"  - الجلسات القادمة: {await _context.LiveSessions.CountAsync(ls => !ls.IsCancelled && ls.ScheduledAt >= DateTime.UtcNow)}");
            sb.AppendLine($"  - الشهادات الصادرة: {await _context.Certificates.CountAsync()}");
            sb.AppendLine($"  - أسئلة AI اليوم: {await _context.AIChatMessages.CountAsync(m => m.IsAnswered && m.CreatedAt >= DateTime.UtcNow.Date)}");
            sb.AppendLine();
        }

        private async Task InjectReceptionistSummaryAsync(StringBuilder sb)
        {
            sb.AppendLine("بيانات الاستقبال:");
            sb.AppendLine($"  - إجمالي المتدربين: {await _context.Trainees.CountAsync()}");
            sb.AppendLine($"  - الدورات المتاحة: {await _context.Courses.CountAsync(c => !c.IsDeleted)}");
            sb.AppendLine($"  - دفعات مسجلة اليوم: {await _context.Payments.CountAsync(p => !p.IsDeleted && p.CreatedDate >= DateTime.UtcNow.Date)}");
            sb.AppendLine();
        }

        // Catch garbage responses from Ollama models that don't handle Arabic well
        private static string SanitizeLlmResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return "عذراً، لم أتمكن من توليد رد. يرجى المحاولة مجدداً.";

            // Detect meta-responses about language switching (common Ollama failure mode)
            if (response.Contains("تغيير اللغة") || response.Contains("change the language")
                || response.Contains("switch to Arabic") || response.Contains("Arabic")
                || response.Length < 12)
                return "عذراً، لم أتمكن من فهم سؤالك بشكل صحيح. يرجى إعادة صياغته وسأحاول مساعدتك.";

            return response;
        }

        private static bool Has(string text, params string[] words)
            => words.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));

        // ─────────────────────────────────────────────────────────────────────
        //  Load conversation history from DB
        // ─────────────────────────────────────────────────────────────────────

        private async Task<List<(string role, string content)>> LoadHistoryAsync(string userId)
        {
            var recent = await _context.AIChatMessages
                .Where(m => m.UserId == userId && m.IsAnswered && m.AIResponse != null)
                .OrderByDescending(m => m.CreatedAt)
                .Take(HistoryPairs)
                .ToListAsync();

            recent.Reverse(); // oldest first

            var result = new List<(string, string)>();
            foreach (var m in recent)
            {
                result.Add(("user",      m.UserMessage));
                result.Add(("assistant", m.AIResponse!));
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static AIQuestionType ClassifyQuestion(string q)
        {
            var l = q.ToLowerInvariant();
            if (l.Contains("درجة") || l.Contains("نتيجة") || l.Contains("grade") || l.Contains("score"))
                return AIQuestionType.DataRequest;
            if (l.Contains("كورس") || l.Contains("دورة") || l.Contains("course"))
                return AIQuestionType.Personal;
            if (l.Contains("انصح") || l.Contains("اقترح") || l.Contains("recommend"))
                return AIQuestionType.Recommendation;
            if (l.Contains("كيف") || l.Contains("ما هو") || l.Contains("شرح") || l.Contains("how"))
                return AIQuestionType.FeatureExplanation;
            return AIQuestionType.General;
        }

        private async Task<AIChatMessage> SaveFailedAsync(
            string userId, string question, string reason, string? role)
        {
            var msg = new AIChatMessage
            {
                UserId               = userId,
                UserMessage          = question,
                AIResponse           = reason,
                IsAnswered           = false,
                RequiresManualReview = true,
                ReviewReason         = reason,
                UserRole             = role
            };
            _context.AIChatMessages.Add(msg);
            await _context.SaveChangesAsync();
            return msg;
        }

        private static string RoleArabic(string role) => role switch
        {
            "Admin"        => "مسؤول",
            "Trainer"      => "مدرب",
            "Trainee"      => "متدرب",
            "Receptionist" => "موظف استقبال",
            _              => role
        };

        // ─────────────────────────────────────────────────────────────────────
        //  Lecture AI Tools  — Summary / Mind Map / Q&A
        //  Shared helper: loads lecture context, builds prompt, calls provider
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// تلخيص محاضرة: يجلب محتوى المحاضرة من قاعدة البيانات
        /// ويطلب من النموذج كتابة ملخص أكاديمي مفصّل وشامل بالعربية.
        /// </summary>
        public async Task<LectureAIAnalysis> SummarizeLectureAsync(string userId, Guid lectureId)
        {
            var (lecture, contextBlock, error) = await BuildLectureContextAsync(lectureId);
            if (error != null)
                return new LectureAIAnalysis { Success = false, Error = error };

            var systemPrompt = """
                أنت أستاذ أكاديمي خبير ومتخصص في إنشاء ملخصات تعليمية عميقة ومفصّلة باللغة العربية.

                مهمتك الأساسية: كتابة ملخص أكاديمي احترافي وشامل جداً يُغني الطالب عن مراجعة المحاضرة من الصفر.

                قواعد إلزامية:
                - اكتب بأسلوب أكاديمي واضح ومنظّم مع شرح كل فكرة بالتفصيل الكافي.
                - كل نقطة يجب أن تكون موسّعة بشرح وافٍ، لا مجرد عنوان.
                - استخدم تنسيق Markdown الكامل (## للعناوين، ### للعناوين الفرعية، ** للنص الغامق، - للقوائم).
                - اجعل الملخص طويلاً ومفيداً بحيث يغطي كل جانب مهم في المحاضرة.
                - أضف أمثلة توضيحية وتشبيهات عند الضرورة لتوضيح المفاهيم الصعبة.
                - لا تختصر أبداً — التفصيل هو الهدف الأساسي.
                """;

            var userPrompt = $"""
                {contextBlock}

                اكتب ملخصاً أكاديمياً مفصّلاً وشاملاً جداً لهذه المحاضرة وفق البنية التالية:

                ---

                ## 📌 نظرة عامة ومقدمة
                اكتب مقدمة موسّعة (فقرتين على الأقل) توضّح:
                - ما هو موضوع المحاضرة وسياقها العلمي/التقني
                - لماذا هذا الموضوع مهم ومرتبط بالمجال العلمي
                - ما الذي سيكتسبه الطالب من دراسة هذا الموضوع
                - علاقة هذه المحاضرة بالمحاضرات الأخرى في الدورة (إن وُجدت)

                ---

                ## 🎯 الأهداف التعليمية التفصيلية
                اذكر جميع الأهداف التعليمية المتوقعة بصياغة "سيكون الطالب قادراً على...":
                - الأهداف المعرفية (فهم المفاهيم والنظريات)
                - الأهداف التطبيقية (تطبيق ما تعلّمه)
                - الأهداف التحليلية (تحليل ومقارنة)
                - مستوى الإتقان المتوقع بعد المحاضرة

                ---

                ## 📚 شرح تفصيلي للمحتوى والمحاور الرئيسية

                لكل محور رئيسي في المحاضرة، اكتب قسماً مستقلاً يتضمن:

                ### [اسم المحور]
                **الشرح الأساسي:**
                [شرح تفصيلي وافٍ للمحور بعدة فقرات، مع الأمثلة والتوضيحات]

                **النقاط الجوهرية:**
                - [نقطة مفصّلة مع شرحها]
                - [نقطة مفصّلة مع شرحها]

                **مثال توضيحي:**
                [مثال عملي أو سيناريو يوضّح الفكرة]

                ← كرّر هذا الهيكل لكل محور رئيسي في المحاضرة

                ---

                ## 🔑 قاموس المصطلحات والمفاهيم الأساسية
                لكل مصطلح مهم:
                - **[المصطلح]:** تعريف دقيق وواضح + مثال على استخدامه + لماذا هو مهم
                - اذكر المصطلح بالعربية والإنجليزية إن وُجد

                ---

                ## ⚙️ الخوارزميات والخطوات والإجراءات
                (إن وُجدت في المحاضرة)
                - اشرح كل خوارزمية أو منهجية أو إجراء خطوةً بخطوة
                - اذكر متى وأين يُطبَّق كل منها
                - اذكر مزايا وعيوب كل نهج مقارنةً بالبدائل

                ---

                ## 🔗 الروابط والعلاقات بين المفاهيم
                - كيف ترتبط مفاهيم هذه المحاضرة ببعضها
                - ما العلاقة بين المحاضرة وما سبقها أو ما سيلحق بها
                - رسم مخطط نصي (بالنقاط) يُظهر التسلسل المنطقي للأفكار

                ---

                ## 💡 التطبيقات العملية ومسائل الاستيعاب
                - أذكر 3-5 تطبيقات عملية حقيقية للمحتوى
                - لكل تطبيق: وصف المشكلة + كيف يحلّها ما تعلّمناه + مثال واقعي
                - أمثلة من الحياة اليومية أو الصناعة تُقرّب المفاهيم

                ---

                ## ⚠️ الأخطاء الشائعة والنقاط الدقيقة
                - أهم الأخطاء التي يقع فيها الطلاب في هذا الموضوع
                - المفاهيم التي يسهل الخلط بينها وكيف نُميّز بينها
                - النقاط الدقيقة التي تحتاج إلى انتباه خاص

                ---

                ## 📝 ملاحظات وتلميحات للمذاكرة
                - نصائح للطالب حول أفضل طريقة لاستيعاب هذا الموضوع
                - ما الذي يجب حفظه وما الذي يجب فهمه دون حفظ
                - الأسئلة التي يجب أن يتساءلها الطالب عند المذاكرة

                ---

                ## ✅ ملخص تنفيذي — أبرز ما يجب تذكره
                نقاط مرقّمة (7-10 نقاط) تُلخّص جوهر المحاضرة بأسلوب واضح ودقيق
                بحيث يمكن للطالب مراجعتها قبيل الامتحان مباشرةً.

                ---

                **تذكّر:** اكتب بتفصيل حقيقي وعمق أكاديمي — هذا الملخص يجب أن يكون مرجعاً شاملاً وكافياً للطالب.
                """;

            // التلخيص يحتاج حد توكنز أعلى لضمان الشمولية
            return await CallLectureAIAsync(userId, lecture!.Title, userPrompt, systemPrompt, "summary", minTokens: 4096);
        }

        /// <summary>
        /// توليد خريطة ذهنية: يطلب من النموذج إنشاء تسلسل هرمي بتنسيق Markdown
        /// يستطيع مكتبة markmap.js تحويله إلى خريطة ذهنية تفاعلية.
        /// </summary>
        public async Task<LectureAIAnalysis> GenerateMindMapAsync(string userId, Guid lectureId)
        {
            // plainFormat=true → uses [معلومات المحاضرة] brackets instead of ## headings
            // to prevent the context header from appearing as a mind-map node.
            var (lecture, contextBlock, error) = await BuildLectureContextAsync(lectureId, plainFormat: true);
            if (error != null)
                return new LectureAIAnalysis { Success = false, Error = error };

            var systemPrompt = """
                أنت مساعد تعليمي متخصص في بناء خرائط ذهنية باللغة العربية.
                مهمتك الوحيدة: إنشاء خريطة ذهنية بتنسيق Markdown هرمي دقيق بناءً على موضوع المحاضرة.

                قواعد صارمة:
                - استخدم # للعنوان الرئيسي (الاسم العلمي أو التقني للموضوع — ليس "معلومات المحاضرة")
                - استخدم ## للأفكار الرئيسية (3-7 أفكار تمثل المحاور الحقيقية للموضوع)
                - استخدم ### للتفاصيل والتفريعات المباشرة لكل فكرة رئيسية
                - استخدم #### للتفاصيل الأعمق عند الضرورة
                - لا تستخدم * أو - أو أي رموز خارج # ## ### ####
                - تحذير مهم: استخدم أسماء المفاهيم الحقيقية للموضوع دائماً
                  لا تكتب أبداً: "تفصيل 1" أو "تفصيل 2" أو "تفصيل أ" أو "فكرة أ" أو أي أرقام عامة
                  كل عقدة يجب أن تحمل اسم المفهوم العلمي الفعلي
                - لا تُدرج أبداً: "معلومات المحاضرة" أو "العنوان" أو "الدورة" أو "التاريخ" كعُقَد
                - الرد يجب أن يكون الخريطة الذهنية فقط، بدون أي نص توضيحي قبلها أو بعدها
                """;

            var userPrompt = $"""
                {contextBlock}

                بناءً على عنوان المحاضرة ووصفها أعلاه، أنشئ خريطة ذهنية شاملة
                تغطي جميع المحاور العلمية والتقنية الرئيسية للموضوع وتفريعاتها الحقيقية.

                مثال على التنسيق والمحتوى الصحيح (هذا مثال على موضوع مختلف فقط لتوضيح البنية — لا تنسخه):
                # تطوير تطبيقات الويب
                ## أساسيات HTML
                ### هيكل المستند
                ### العناصر الدلالية
                ### النماذج والمدخلات
                ## تنسيق CSS
                ### نموذج الصندوق
                ### Flexbox وGrid
                ### المتغيرات CSS
                ## JavaScript التفاعلي
                ### التعامل مع DOM
                ### معالجة الأحداث
                #### أحداث النقر
                #### الطلبات غير المتزامنة

                الآن أنشئ الخريطة الذهنية لمحاضرتك (بمصطلحات ومفاهيم حقيقية من موضوع المحاضرة):
                """;

            return await CallLectureAIAsync(userId, lecture!.Title, userPrompt, systemPrompt, "mindmap");
        }

        /// <summary>
        /// توليد أسئلة وأجوبة: يطلب من النموذج إنشاء مجموعة متنوعة من الأسئلة
        /// تشمل اختيار متعدد وصح/خطأ وأسئلة مفتوحة عن محتوى المحاضرة.
        /// </summary>
        public async Task<LectureAIAnalysis> GenerateQnAAsync(string userId, Guid lectureId)
        {
            var (lecture, contextBlock, error) = await BuildLectureContextAsync(lectureId);
            if (error != null)
                return new LectureAIAnalysis { Success = false, Error = error };

            var systemPrompt = """
                أنت مساعد تعليمي خبير في تصميم الاختبارات والأسئلة التعليمية.
                مهمتك: توليد أسئلة وأجوبة متنوعة وعالية الجودة باللغة العربية
                تقيس الفهم الحقيقي للمحاضرة وليس الحفظ فقط.
                """;

            var userPrompt = $"""
                {contextBlock}

                ولّد مجموعة شاملة من الأسئلة والأجوبة عن هذه المحاضرة تتضمن:

                ## 🔵 أسئلة اختيار من متعدد (5 أسئلة)
                لكل سؤال: 4 خيارات (أ، ب، ج، د) مع تحديد الإجابة الصحيحة وشرحها.

                ## ✅ أسئلة صح / خطأ (5 أسئلة)
                جمل يحدد فيها الطالب إذا كانت صحيحة أو خاطئة مع التصحيح.

                ## 💬 أسئلة مفتوحة (5 أسئلة)
                أسئلة تتطلب تفكيراً وشرحاً مع إجابة نموذجية مفصّلة.

                ## 🏆 سؤال تطبيقي (1 سؤال)
                سؤال تطبيقي عملي أو سيناريو واقعي يختبر القدرة على التطبيق.

                نسّق كل سؤال بشكل واضح باستخدام Markdown وأيقونات مناسبة.
                """;

            return await CallLectureAIAsync(userId, lecture!.Title, userPrompt, systemPrompt, "qna");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Private: Lecture data loader + AI caller
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// يجلب بيانات المحاضرة من قاعدة البيانات ويبني كتلة سياق نصية
        /// للحقن في نموذج الذكاء الاصطناعي.
        /// </summary>
        /// <param name="plainFormat">
        /// عندما تكون true يُستخدم تنسيق [أقواس] بدلاً من ## Markdown
        /// لمنع عناوين كتلة السياق من الظهور كعقد في الخريطة الذهنية.
        /// </param>
        private async Task<(Lecture? lecture, string contextBlock, string? error)>
            BuildLectureContextAsync(Guid lectureId, bool plainFormat = false)
        {
            var lecture = await _context.Lectures
                .Include(l => l.Course)
                .Include(l => l.Resources.Where(r => r.IsVisible && !r.IsDeleted))
                .Include(l => l.Materials)
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LectureId == lectureId && !l.IsDeleted);

            if (lecture == null)
                return (null, "", "المحاضرة غير موجودة أو تم حذفها.");

            var sb = new StringBuilder();
            // Use plain brackets instead of ## when called for mind-map generation,
            // so the context header is never mistaken for a mind-map node by the LLM.
            if (plainFormat)
                sb.AppendLine("[معلومات المحاضرة التعليمية]");
            else
                sb.AppendLine("## معلومات المحاضرة التعليمية");
            sb.AppendLine($"**العنوان:** {lecture.Title}");
            sb.AppendLine($"**الدورة:** {lecture.Course?.CourseName ?? "—"}");
            sb.AppendLine($"**التاريخ:** {lecture.LectureDate:dd/MM/yyyy}");

            if (!string.IsNullOrWhiteSpace(lecture.Description))
            {
                sb.AppendLine();
                sb.AppendLine("**وصف المحاضرة:**");
                sb.AppendLine(lecture.Description);
            }

            // Resources (uploaded files, materials)
            var resources = lecture.Resources?.ToList() ?? new List<LectureResource>();
            if (resources.Any())
            {
                sb.AppendLine();
                sb.AppendLine($"**الملفات والمواد المرفقة ({resources.Count} ملف):**");
                foreach (var r in resources)
                {
                    var typeLabel = r.ResourceType switch
                    {
                        ResourceType.LectureSlides => "شرائح المحاضرة",
                        ResourceType.Notes         => "ملاحظات",
                        ResourceType.Assignment    => "واجب",
                        ResourceType.Solution      => "حل",
                        ResourceType.Reference     => "مراجع",
                        ResourceType.Code          => "أكواد",
                        ResourceType.ProjectFiles  => "ملفات مشروع",
                        _                          => "ملف"
                    };
                    sb.AppendLine($"- [{typeLabel}] {r.FileName}" +
                                  (!string.IsNullOrWhiteSpace(r.Description) ? $" — {r.Description}" : ""));
                }
            }

            // Legacy materials
            var mats = lecture.Materials?.Where(m => !m.IsDeleted).ToList() ?? new List<LectureMaterial>();
            if (mats.Any())
            {
                sb.AppendLine();
                sb.AppendLine($"**مواد المحاضرة ({mats.Count}):**");
                foreach (var m in mats)
                    sb.AppendLine($"- {m.Title} ({m.ContentType})");
            }

            return (lecture, sb.ToString(), null);
        }

        /// <summary>
        /// يستدعي مزود الذكاء الاصطناعي المُعيَّن (Claude/OpenAI/Groq/Ollama)
        /// بسياق المحاضرة المُبنَّى ويُعيد النتيجة.
        /// </summary>
        private async Task<LectureAIAnalysis> CallLectureAIAsync(
            string userId, string lectureTitle,
            string userPrompt, string systemPrompt,
            string analysisType,
            int minTokens = 2048)          // ← المُلخِّص يمرر 4096، الباقون يبقون على 2048
        {
            try
            {
                var cfg = await GetSystemConfigAsync();
                if (!cfg.IsEnabled)
                    return new LectureAIAnalysis
                    {
                        Success = false,
                        Error   = "المساعد الذكي معطّل حالياً."
                    };

                var anthropicKey = ResolveApiKey(cfg.AnthropicApiKey, _systemApiKey);
                var openAiKey    = ResolveApiKey(cfg.OpenAIApiKey,    _openAiApiKey);
                var groqKey      = ResolveApiKey(cfg.GroqApiKey,      _groqApiKey);

                // استخدم أعلى قيمة: إعداد النظام، أو الحد الأدنى المطلوب للمهمة
                var maxTokens = Math.Max(cfg.MaxTokensPerResponse, minTokens);
                string content;
                string provider;

                var history = new List<(string, string)>(); // no chat history needed here

                // ── Lecture AI timeout = 15 minutes for every provider ──────────
                // Lecture analysis (especially summary) generates thousands of tokens
                // and is much slower than a chat turn. 15 min gives all models
                // enough headroom without waiting forever if something truly breaks.
                const int LectureTimeoutMinutes = 15;

                switch (cfg.Provider)
                {
                    case AIProviderType.Ollama:
                        // Extra settings for Ollama: wider context window for big prompts,
                        // and numPredict tells the model the minimum expected output length.
                        content  = await CallOllamaAsync(
                                       systemPrompt, history, userPrompt,
                                       cfg.OllamaUrl, cfg.OllamaModel,
                                       timeoutMinutes: LectureTimeoutMinutes,
                                       numCtx:         32768,
                                       numPredict:     minTokens);
                        provider = $"Ollama ({cfg.OllamaModel})";
                        break;

                    case AIProviderType.Groq:
                        content  = await CallOpenAIAsync(systemPrompt, history, userPrompt,
                                       groqKey, cfg.GroqModel, maxTokens, GroqBaseUrl,
                                       timeoutMinutes: LectureTimeoutMinutes);
                        provider = $"Groq ({cfg.GroqModel})";
                        break;

                    case AIProviderType.OpenAI:
                        content  = await CallOpenAIAsync(systemPrompt, history, userPrompt,
                                       openAiKey, cfg.OpenAIModel, maxTokens, OpenAIBaseUrl,
                                       timeoutMinutes: LectureTimeoutMinutes);
                        provider = $"OpenAI ({cfg.OpenAIModel})";
                        break;

                    default: // Anthropic
                        content  = await CallAnthropicAsync(systemPrompt, history, userPrompt,
                                       anthropicKey, maxTokens,
                                       timeoutMinutes: LectureTimeoutMinutes);
                        provider = "Claude (Anthropic)";
                        break;
                }

                _logger.LogInformation(
                    "LectureAI [{Type}] for lecture '{Title}' via {Provider}",
                    analysisType, lectureTitle, provider);

                return new LectureAIAnalysis
                {
                    Success      = true,
                    Content      = content,
                    LectureTitle = lectureTitle,
                    Provider     = provider,
                    AnalysisType = analysisType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LectureAI [{Type}] failed: {Message}", analysisType, ex.Message);

                var userMsg = ex.Message.Contains("401") || ex.Message.Contains("Unauthorized")
                    ? "المفتاح غير صالح. يرجى تحديث إعدادات الذكاء الاصطناعي."
                    : ex.Message.Contains("429") || ex.Message.Contains("rate_limit")
                        ? "تجاوزت الحد اليومي لـ API. يرجى المحاولة لاحقاً."
                        : $"حدث خطأ أثناء معالجة الطلب. يرجى المحاولة مجدداً.";

                return new LectureAIAnalysis
                {
                    Success      = false,
                    Error        = userMsg,
                    LectureTitle = lectureTitle,
                    AnalysisType = analysisType
                };
            }
        }
    }
}
