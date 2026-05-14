using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Text.Json;
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

        private const string AnthropicModel = "claude-sonnet-4-6";
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
            _systemApiKey = configuration["Anthropic:ApiKey"] ?? "";
        }

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
                // ── Layer 1: Direct Response Engine ──────────────────────────
                // For factual data queries, build the answer directly from the DB.
                // This bypasses the LLM entirely — small models cannot reliably
                // read structured data injected into prompts.
                var direct = await TryDirectResponseAsync(userId, role, user, question);
                if (direct != null)
                {
                    var directMsg = new AIChatMessage
                    {
                        UserId        = userId,
                        UserMessage   = question,
                        AIResponse    = direct,
                        QuestionType  = ClassifyQuestion(question),
                        IsAnswered    = true,
                        UserRole      = role,
                        DataAccessLog = "DirectEngine",
                        AnsweredAt    = DateTime.UtcNow
                    };
                    _context.AIChatMessages.Add(directMsg);
                    await _context.SaveChangesAsync();
                    await _permissions.LogAccessAsync(userId, "READ", "DirectEngine", null, true, null, ipAddress, userAgent);
                    return directMsg;
                }

                // ── Layer 2: LLM (general / conversational questions) ─────────
                var systemPrompt = BuildCoreSystemPrompt(user, role);
                var history      = await LoadHistoryAsync(userId);
                var contextualQ  = await BuildContextualUserMessageAsync(userId, role, user, question);

                string aiResponse = cfg.Provider == AIProviderType.Ollama
                    ? await CallOllamaAsync(systemPrompt, history, contextualQ, cfg.OllamaUrl, cfg.OllamaModel)
                    : await CallAnthropicAsync(systemPrompt, history, contextualQ, _systemApiKey, cfg.MaxTokensPerResponse);

                var providerLabel = cfg.Provider == AIProviderType.Ollama
                    ? $"Ollama ({cfg.OllamaModel})"
                    : "Claude (Anthropic)";

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
                _logger.LogError(ex, "AI call failed for user {UserId}", userId);
                return await SaveFailedAsync(userId, question,
                    "عذراً، حدث خطأ في الاتصال بالمساعد الذكي. يرجى المحاولة مجدداً.", role);
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

            bool isCourseQ  = q.Contains("دور")   || q.Contains("كورس")  || q.Contains("مقرر")  || q.Contains("course",   StringComparison.OrdinalIgnoreCase);
            bool isAttendQ  = q.Contains("حضو")   || q.Contains("غياب")  || q.Contains("attend", StringComparison.OrdinalIgnoreCase);
            bool isExamQ    = q.Contains("امتحا") || q.Contains("نتيج")  || q.Contains("درجة")  || q.Contains("علامة")  || q.Contains("ناجح")  || q.Contains("راسب") || q.Contains("اختبا") || q.Contains("exam", StringComparison.OrdinalIgnoreCase);
            bool isPayQ     = q.Contains("دفع")   || q.Contains("فاتور") || q.Contains("مبلغ")  || q.Contains("رسوم")   || q.Contains("سدد")   || q.Contains("payment", StringComparison.OrdinalIgnoreCase);
            bool isSessionQ = q.Contains("جلسة")  || q.Contains("مباشر") || q.Contains("لايف")  || q.Contains("موعد")   || q.Contains("session", StringComparison.OrdinalIgnoreCase);
            bool isCertQ    = q.Contains("شهادة") || q.Contains("شهادا") || q.Contains("certificate", StringComparison.OrdinalIgnoreCase);
            bool isStatsQ   = q.Contains("إحصا")  || q.Contains("عدد ")  || q.Contains("تقرير") || q.Contains("إيراد")  || q.Contains("مجموع")  || q.Contains("statistics", StringComparison.OrdinalIgnoreCase);

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
                    if (isStatsQ || isCourseQ || isPayQ)
                        return await DirectAdminStatsAsync(user);
                    break;

                case "Receptionist":
                    if (isCourseQ || isPayQ || isStatsQ)
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
            var trainees  = await _context.Trainees.CountAsync();
            var trainers  = await _context.Trainers.CountAsync();
            var courses   = await _context.Courses.CountAsync(c => !c.IsDeleted);
            var exams     = await _context.Exams.CountAsync();
            var revenue   = await _context.Payments.Where(p => !p.IsDeleted).SumAsync(p => p.TotalAmount);
            var sessions  = await _context.LiveSessions.CountAsync(ls => !ls.IsCancelled && ls.ScheduledAt >= DateTime.UtcNow);
            var certs     = await _context.Certificates.CountAsync();
            var aiToday   = await _context.AIChatMessages.CountAsync(m => m.IsAnswered && m.CreatedAt >= DateTime.UtcNow.Date);

            return $"""
                مرحباً {user.FullName}! إليك إحصائيات المركز الحالية:

                المتدربون المسجلون : {trainees}
                المدربون           : {trainers}
                الدورات النشطة     : {courses}
                الامتحانات         : {exams}
                الشهادات الصادرة   : {certs}
                الجلسات القادمة    : {sessions}
                إجمالي الإيرادات   : {revenue:N0}
                أسئلة AI اليوم     : {aiToday}
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
        //  Claude API — raw HTTP call
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string> CallAnthropicAsync(
            string systemPrompt,
            List<(string role, string content)> history,
            string question,
            string apiKey,
            int maxTokens = 1024)
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

        private async Task<string> CallOllamaAsync(
            string systemPrompt,
            List<(string role, string content)> history,
            string question,
            string ollamaUrl,
            string ollamaModel)
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
                    temperature = 0.3,   // more deterministic → fewer hallucinations
                    num_ctx     = 8192,  // enough room for full system prompt + data
                    top_p       = 0.9
                }
            });

            var http    = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromMinutes(3);
            var request = new HttpRequestMessage(
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
        //  Core System Prompt  (minimal — identity + behavior only)
        // ─────────────────────────────────────────────────────────────────────

        private static string BuildCoreSystemPrompt(ApplicationUser user, string role)
        {
            return $"""
                أنت مساعد ذكي اسمه "مساعد المركز" مدمج في نظام إدارة مركز تدريب.
                المستخدم الحالي: {user.FullName} ({RoleArabic(role)}).

                تعليمات إلزامية:
                - تحدث دائماً بالعربية بأسلوب ودي ومهني.
                - عندما يحتوي السؤال على بيانات بين [بيانات النظام] و [/بيانات النظام]، اقرأها واستخدمها مباشرةً في إجابتك.
                - لا تقل أبداً "لا يمكنني الوصول إلى بياناتك" — البيانات موجودة في السؤال ذاته.
                - لا تخترع أرقاماً أو معلومات غير مُدرجة.
                - استخدم القوائم عند عرض بيانات متعددة.
                """;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Contextual User Message Builder  (RAG — data injected into message)
        // ─────────────────────────────────────────────────────────────────────

        private async Task<string> BuildContextualUserMessageAsync(
            string userId, string role, ApplicationUser user, string question)
        {
            // This method is only called for Layer-2 (general/conversational) questions.
            // We inject knowledge base entries and the user identity as readable prose.
            var sb = new StringBuilder();

            var kb = await _context.AIKnowledgeEntries
                .Where(e => e.IsActive)
                .OrderBy(e => e.SortOrder).ThenBy(e => e.CreatedAt)
                .AsNoTracking().Take(8)
                .ToListAsync();

            if (kb.Any())
            {
                sb.AppendLine("معلومات عامة عن مركز التدريب:");
                foreach (var g in kb.GroupBy(k => k.Category))
                {
                    sb.AppendLine($"[{g.Key}]");
                    foreach (var k in g)
                        sb.AppendLine($"- {k.Title}: {k.Content}");
                }
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            sb.AppendLine($"المستخدم: {user.FullName} ({RoleArabic(role)})");
            sb.AppendLine();
            sb.AppendLine($"السؤال: {question}");
            sb.AppendLine();
            sb.AppendLine("أجب بالعربية بأسلوب ودي ومهني:");

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
    }
}
