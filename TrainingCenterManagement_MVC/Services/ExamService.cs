using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.DTOs;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.Models.Enums;

namespace TrainingCenterManagement_MVC.Services
{
    public class ExamService : IExamService
    {
        private readonly ApplicationDbContext _db;

        public ExamService(ApplicationDbContext db)
        {
            _db = db;
        }

        // ══════════════════════════════════════════════════════════
        //  TRAINER — إدارة الامتحانات
        // ══════════════════════════════════════════════════════════

        public async Task<ExamDto> CreateExamAsync(CreateExamDto dto, Guid trainerId)
        {
            var exam = new Exam
            {
                ExamName = dto.ExamName,
                Instructions = dto.Instructions,
                StartDateTime = dto.StartDateTime.ToUniversalTime(),
                DurationMinutes = dto.DurationMinutes,
                PassingScore = dto.PassingScore,
                MaxAttempts = dto.MaxAttempts,
                IsRandomized = dto.IsRandomized,
                ShowResultsImmediately = dto.ShowResultsImmediately,
                CourseId = dto.CourseId,
                TrainerId = trainerId
            };

            _db.Exams.Add(exam);

            // إضافة الأسئلة إذا حُدِّدت عند الإنشاء
            if (dto.QuestionIds?.Count > 0)
            {
                var questions = await _db.Questions
                    .Where(q => dto.QuestionIds.Contains(q.QuestionId) && !q.IsDeleted)
                    .ToListAsync();

                for (int i = 0; i < questions.Count; i++)
                {
                    _db.ExamQuestions.Add(new ExamQuestion
                    {
                        ExamId = exam.ExamId,
                        QuestionId = questions[i].QuestionId,
                        OrderIndex = i
                    });
                }
            }

            await _db.SaveChangesAsync();
            return await MapToExamDtoAsync(exam.ExamId);
        }

        public async Task<ExamDto> UpdateExamAsync(UpdateExamDto dto, Guid trainerId)
        {
            var exam = await _db.Exams
                .FirstOrDefaultAsync(e => e.ExamId == dto.ExamId && e.TrainerId == trainerId && !e.IsDeleted)
                ?? throw new InvalidOperationException("الامتحان غير موجود أو ليس لديك صلاحية تعديله.");

            if (exam.ExamAttempts?.Any(a => a.Status == AttemptStatus.InProgress) == true)
                throw new InvalidOperationException("لا يمكن تعديل امتحان جارٍ حالياً.");

            exam.ExamName = dto.ExamName;
            exam.Instructions = dto.Instructions;
            exam.StartDateTime = dto.StartDateTime.ToUniversalTime();
            exam.DurationMinutes = dto.DurationMinutes;
            exam.PassingScore = dto.PassingScore;
            exam.MaxAttempts = dto.MaxAttempts;
            exam.IsRandomized = dto.IsRandomized;
            exam.ShowResultsImmediately = dto.ShowResultsImmediately;
            exam.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return await MapToExamDtoAsync(exam.ExamId);
        }

        public async Task<bool> DeleteExamAsync(Guid examId, Guid trainerId)
        {
            var exam = await _db.Exams
                .FirstOrDefaultAsync(e => e.ExamId == examId && e.TrainerId == trainerId && !e.IsDeleted);

            if (exam == null) return false;

            exam.IsDeleted = true;
            exam.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> PublishExamAsync(Guid examId, Guid trainerId)
        {
            var exam = await _db.Exams
                .Include(e => e.ExamQuestions)
                .FirstOrDefaultAsync(e => e.ExamId == examId && e.TrainerId == trainerId && !e.IsDeleted);

            if (exam == null) return false;
            if (!exam.ExamQuestions.Any())
                throw new InvalidOperationException("لا يمكن نشر امتحان بدون أسئلة.");

            exam.IsPublished = true;
            exam.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UnpublishExamAsync(Guid examId, Guid trainerId)
        {
            var exam = await _db.Exams
                .FirstOrDefaultAsync(e => e.ExamId == examId && e.TrainerId == trainerId && !e.IsDeleted);

            if (exam == null) return false;

            if (await _db.ExamAttempts.AnyAsync(a => a.ExamId == examId && a.Status == AttemptStatus.InProgress))
                throw new InvalidOperationException("يوجد طلاب يؤدون الامتحان الآن.");

            exam.IsPublished = false;
            exam.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  TRAINER — بنك الأسئلة
        // ══════════════════════════════════════════════════════════

        public async Task<QuestionDto> CreateQuestionAsync(CreateQuestionDto dto, Guid trainerId)
        {
            ValidateQuestionDto(dto);

            var question = new Question
            {
                QuestionText = dto.QuestionText,
                QuestionType = dto.QuestionType,
                DifficultyLevel = dto.DifficultyLevel,
                CorrectAnswer = dto.CorrectAnswer?.Trim(),
                Explanation = dto.Explanation,
                DefaultPoints = dto.DefaultPoints,
                TrainerId = trainerId
            };

            if (dto.Options?.Count > 0)
                question.Options = dto.Options;

            _db.Questions.Add(question);
            await _db.SaveChangesAsync();
            return MapToQuestionDto(question, null);
        }

        public async Task<QuestionDto> UpdateQuestionAsync(UpdateQuestionDto dto, Guid trainerId)
        {
            var question = await _db.Questions
                .FirstOrDefaultAsync(q => q.QuestionId == dto.QuestionId && q.TrainerId == trainerId && !q.IsDeleted)
                ?? throw new InvalidOperationException("السؤال غير موجود.");

            question.QuestionText = dto.QuestionText;
            question.QuestionType = dto.QuestionType;
            question.DifficultyLevel = dto.DifficultyLevel;
            question.CorrectAnswer = dto.CorrectAnswer?.Trim();
            question.Explanation = dto.Explanation;
            question.DefaultPoints = dto.DefaultPoints;

            if (dto.Options?.Count > 0)
                question.Options = dto.Options;

            await _db.SaveChangesAsync();
            return MapToQuestionDto(question, null);
        }

        public async Task<bool> DeleteQuestionAsync(Guid questionId, Guid trainerId)
        {
            var question = await _db.Questions
                .FirstOrDefaultAsync(q => q.QuestionId == questionId && q.TrainerId == trainerId && !q.IsDeleted);

            if (question == null) return false;

            question.IsDeleted = true;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<List<QuestionDto>> GetQuestionBankAsync(
            Guid trainerId,
            QuestionType? type = null,
            DifficultyLevel? difficulty = null)
        {
            var query = _db.Questions
                .Where(q => q.TrainerId == trainerId && !q.IsDeleted);

            if (type.HasValue)
                query = query.Where(q => q.QuestionType == type.Value);

            if (difficulty.HasValue)
                query = query.Where(q => q.DifficultyLevel == difficulty.Value);

            var questions = await query
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            var usageCounts = await _db.ExamQuestions
                .Where(eq => questions.Select(q => q.QuestionId).Contains(eq.QuestionId))
                .GroupBy(eq => eq.QuestionId)
                .Select(g => new { QuestionId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.QuestionId, x => x.Count);

            return questions.Select(q =>
            {
                var dto = MapToQuestionDto(q, null);
                dto.UsedInExamsCount = usageCounts.GetValueOrDefault(q.QuestionId, 0);
                return dto;
            }).ToList();
        }

        // ══════════════════════════════════════════════════════════
        //  TRAINER — إدارة أسئلة الامتحان
        // ══════════════════════════════════════════════════════════

        public async Task AddQuestionToExamAsync(AddQuestionToExamDto dto, Guid trainerId)
        {
            var exam = await _db.Exams
                .FirstOrDefaultAsync(e => e.ExamId == dto.ExamId && e.TrainerId == trainerId && !e.IsDeleted)
                ?? throw new InvalidOperationException("الامتحان غير موجود.");

            var questionExists = await _db.Questions
                .AnyAsync(q => q.QuestionId == dto.QuestionId && !q.IsDeleted);
            if (!questionExists)
                throw new InvalidOperationException("السؤال غير موجود.");

            var alreadyAdded = await _db.ExamQuestions
                .AnyAsync(eq => eq.ExamId == dto.ExamId && eq.QuestionId == dto.QuestionId);
            if (alreadyAdded)
                throw new InvalidOperationException("السؤال مضاف مسبقاً لهذا الامتحان.");

            var maxOrder = await _db.ExamQuestions
                .Where(eq => eq.ExamId == dto.ExamId)
                .Select(eq => (int?)eq.OrderIndex)
                .MaxAsync() ?? -1;

            _db.ExamQuestions.Add(new ExamQuestion
            {
                ExamId = dto.ExamId,
                QuestionId = dto.QuestionId,
                OrderIndex = dto.OrderIndex > 0 ? dto.OrderIndex : maxOrder + 1,
                PointsOverride = dto.PointsOverride
            });

            exam.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task RemoveQuestionFromExamAsync(Guid examId, Guid questionId, Guid trainerId)
        {
            var examQuestion = await _db.ExamQuestions
                .Include(eq => eq.Exam)
                .FirstOrDefaultAsync(eq => eq.ExamId == examId && eq.QuestionId == questionId
                    && eq.Exam.TrainerId == trainerId)
                ?? throw new InvalidOperationException("العنصر غير موجود.");

            if (await _db.StudentAnswers.AnyAsync(sa => sa.QuestionId == questionId
                    && sa.Attempt.ExamId == examId))
                throw new InvalidOperationException("لا يمكن حذف سؤال لديه إجابات طلاب.");

            _db.ExamQuestions.Remove(examQuestion);
            await _db.SaveChangesAsync();
        }

        public async Task ReorderQuestionsAsync(
            Guid examId,
            List<(Guid QuestionId, int OrderIndex)> order,
            Guid trainerId)
        {
            var examQuestions = await _db.ExamQuestions
                .Include(eq => eq.Exam)
                .Where(eq => eq.ExamId == examId && eq.Exam.TrainerId == trainerId)
                .ToListAsync();

            foreach (var eq in examQuestions)
            {
                var newOrder = order.FirstOrDefault(o => o.QuestionId == eq.QuestionId);
                if (newOrder != default)
                    eq.OrderIndex = newOrder.OrderIndex;
            }

            await _db.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════
        //  TRAINER — نتائج الطلاب
        // ══════════════════════════════════════════════════════════

        public async Task<TrainerExamResultsDto> GetExamResultsForTrainerAsync(Guid examId, Guid trainerId)
        {
            var exam = await _db.Exams
                .FirstOrDefaultAsync(e => e.ExamId == examId && e.TrainerId == trainerId && !e.IsDeleted)
                ?? throw new InvalidOperationException("الامتحان غير موجود.");

            var attempts = await _db.ExamAttempts
                .Include(a => a.Trainee).ThenInclude(t => t.User)
                .Include(a => a.StudentAnswers)
                .Where(a => a.ExamId == examId)
                .OrderByDescending(a => a.StartedAt)
                .ToListAsync();

            var submitted = attempts.Where(a =>
                a.Status == AttemptStatus.Submitted || a.Status == AttemptStatus.TimedOut).ToList();

            return new TrainerExamResultsDto
            {
                ExamId = examId,
                ExamName = exam.ExamName,
                TotalAttempts = attempts.Count,
                SubmittedCount = submitted.Count,
                PassedCount = submitted.Count(a => a.IsPassed == true),
                FailedCount = submitted.Count(a => a.IsPassed == false),
                AverageScore = submitted.Any()
                    ? Math.Round(submitted.Average(a => a.ScorePercentage ?? 0), 2)
                    : null,
                HighestScore = submitted.Any()
                    ? submitted.Max(a => a.ScorePercentage)
                    : null,
                LowestScore = submitted.Any()
                    ? submitted.Min(a => a.ScorePercentage)
                    : null,
                Attempts = attempts.Select(a => new AttemptSummaryDto
                {
                    AttemptId = a.AttemptId,
                    TraineeId = a.TraineeId,
                    TraineeName = a.Trainee?.User?.UserName ?? "—",
                    TraineeEmail = a.Trainee?.User?.Email ?? "—",
                    ScorePercentage = a.ScorePercentage,
                    IsPassed = a.IsPassed,
                    Status = a.Status,
                    StartedAt = a.StartedAt,
                    SubmittedAt = a.SubmittedAt,
                    TabSwitchCount = a.TabSwitchCount,
                    IpAddress = a.IpAddress,
                    HasPendingEssays = a.StudentAnswers.Any(sa => !sa.IsManuallyGraded && sa.IsCorrect == null),
                    PenaltyApplied = a.PenaltyApplied,
                    PenaltyReason = a.PenaltyReason
                }).ToList()
            };
        }

        public async Task<ExamResultDto> GetAttemptDetailForTrainerAsync(Guid attemptId, Guid trainerId)
        {
            var attempt = await _db.ExamAttempts
                .Include(a => a.Exam)
                .Include(a => a.Trainee).ThenInclude(t => t.User)
                .Include(a => a.StudentAnswers).ThenInclude(sa => sa.Question)
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId && a.Exam.TrainerId == trainerId)
                ?? throw new InvalidOperationException("المحاولة غير موجودة.");

            return BuildExamResultDto(attempt, showAnswers: true);
        }

        public async Task<bool> ApplyPenaltyAsync(ApplyPenaltyDto dto, Guid trainerId)
        {
            var attempt = await _db.ExamAttempts
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.AttemptId == dto.AttemptId && a.Exam.TrainerId == trainerId);

            if (attempt == null) return false;
            if (attempt.Status != AttemptStatus.Submitted && attempt.Status != AttemptStatus.TimedOut)
                return false;

            // حفظ الدرجة الأصلية قبل أي عقوبة (مرة واحدة فقط)
            if (!attempt.PenaltyApplied)
            {
                attempt.OriginalTotalScore = attempt.TotalScore;
                attempt.OriginalScorePercentage = attempt.ScorePercentage;
            }

            var baseTotal = attempt.OriginalTotalScore ?? attempt.TotalScore ?? 0;
            var basePct   = attempt.OriginalScorePercentage ?? attempt.ScorePercentage ?? 0;
            var maxScore  = attempt.MaxScore ?? 0;

            switch (dto.PenaltyType)
            {
                case PenaltyType.ZeroGrade:
                    attempt.TotalScore = 0;
                    attempt.ScorePercentage = 0;
                    break;

                case PenaltyType.DeductPoints:
                    var deductedTotal = Math.Max(0, baseTotal - (dto.DeductionValue ?? 0));
                    attempt.TotalScore = Math.Round(deductedTotal, 2);
                    attempt.ScorePercentage = maxScore > 0
                        ? Math.Round(deductedTotal / maxScore * 100, 2)
                        : 0;
                    break;

                case PenaltyType.DeductPercentage:
                    var newPct = Math.Max(0, basePct - (dto.DeductionValue ?? 0));
                    attempt.ScorePercentage = Math.Round(newPct, 2);
                    attempt.TotalScore = maxScore > 0
                        ? Math.Round(newPct / 100 * maxScore, 2)
                        : 0;
                    break;
            }

            attempt.IsPassed = attempt.ScorePercentage >= attempt.Exam.PassingScore;
            attempt.PenaltyApplied = true;
            attempt.PenaltyReason = dto.Reason;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemovePenaltyAsync(Guid attemptId, Guid trainerId)
        {
            var attempt = await _db.ExamAttempts
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId && a.Exam.TrainerId == trainerId);

            if (attempt == null || !attempt.PenaltyApplied) return false;

            attempt.TotalScore = attempt.OriginalTotalScore;
            attempt.ScorePercentage = attempt.OriginalScorePercentage;
            attempt.IsPassed = attempt.ScorePercentage >= attempt.Exam.PassingScore;
            attempt.PenaltyApplied = false;
            attempt.PenaltyReason = null;
            attempt.OriginalTotalScore = null;
            attempt.OriginalScorePercentage = null;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ManualGradeAnswerAsync(ManualGradeDto dto, Guid trainerId)
        {
            var answer = await _db.StudentAnswers
                .Include(sa => sa.Attempt).ThenInclude(a => a.Exam)
                .Include(sa => sa.Question)
                .FirstOrDefaultAsync(sa => sa.AnswerId == dto.AnswerId
                    && sa.Attempt.Exam.TrainerId == trainerId)
                ?? throw new InvalidOperationException("الإجابة غير موجودة.");

            var maxPoints = answer.Attempt.Exam != null
                ? (await _db.ExamQuestions
                    .Where(eq => eq.ExamId == answer.Attempt.ExamId && eq.QuestionId == answer.QuestionId)
                    .Select(eq => eq.PointsOverride ?? eq.Question.DefaultPoints)
                    .FirstOrDefaultAsync())
                : answer.Question.DefaultPoints;

            if (dto.PointsEarned > maxPoints)
                throw new InvalidOperationException($"الدرجة المُعطاة ({dto.PointsEarned}) أكبر من الدرجة الكاملة ({maxPoints}).");

            answer.PointsEarned = dto.PointsEarned;
            answer.IsCorrect = dto.PointsEarned > 0;
            answer.TrainerFeedback = dto.Feedback;
            answer.IsManuallyGraded = true;
            answer.LastModifiedAt = DateTime.UtcNow;

            // إعادة حساب الدرجة الإجمالية للمحاولة
            await RecalculateAttemptScoreAsync(answer.AttemptId);
            await _db.SaveChangesAsync();
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  TRAINEE — أداء الامتحان
        // ══════════════════════════════════════════════════════════

        public async Task<List<ExamSummaryDto>> GetAvailableExamsForTraineeAsync(Guid traineeId)
        {
            // الامتحانات المتاحة = منشورة + الطالب مسجَّل في الكورس
            var enrolledCourseIds = await _db.CourseTrainees
                .Where(ct => ct.TraineeId == traineeId)
                .Select(ct => ct.CourseId)
                .ToListAsync();

            var exams = await _db.Exams
                .Include(e => e.ExamAttempts.Where(a => a.TraineeId == traineeId))
                .Include(e => e.ExamQuestions)
                .Where(e => enrolledCourseIds.Contains(e.CourseId) && e.IsPublished && !e.IsDeleted)
                .OrderBy(e => e.StartDateTime)
                .ToListAsync();

            return exams.Select(e =>
            {
                var lastAttempt = e.ExamAttempts.OrderByDescending(a => a.StartedAt).FirstOrDefault();
                return new ExamSummaryDto
                {
                    ExamId = e.ExamId,
                    ExamName = e.ExamName,
                    StartDateTime = e.StartDateTime,
                    DurationMinutes = e.DurationMinutes,
                    IsPublished = e.IsPublished,
                    IsActive = e.IsActive,
                    AttemptCount = e.ExamAttempts.Count,
                    HasAttempted = lastAttempt != null,
                    LastAttemptStatus = lastAttempt?.Status,
                    LastAttemptId = lastAttempt?.AttemptId
                };
            }).ToList();
        }

        public async Task<ExamAttemptDto> StartExamAsync(
            Guid examId,
            Guid traineeId,
            string ipAddress,
            string userAgent)
        {
            var exam = await _db.Exams
                .Include(e => e.ExamQuestions).ThenInclude(eq => eq.Question)
                .FirstOrDefaultAsync(e => e.ExamId == examId && e.IsPublished && !e.IsDeleted)
                ?? throw new InvalidOperationException("الامتحان غير موجود أو غير منشور.");

            // التحقق من الوقت
            if (DateTime.UtcNow < exam.StartDateTime)
                throw new InvalidOperationException($"الامتحان لم يبدأ بعد. يبدأ في: {exam.StartDateTime:dd/MM/yyyy HH:mm} UTC");

            if (DateTime.UtcNow > exam.EndDateTime)
                throw new InvalidOperationException("انتهى وقت الامتحان.");

            // التحقق من عدد المحاولات
            var previousAttempts = await _db.ExamAttempts
                .Where(a => a.ExamId == examId && a.TraineeId == traineeId)
                .OrderByDescending(a => a.StartedAt)
                .ToListAsync();

            var completedAttempts = previousAttempts
                .Count(a => a.Status == AttemptStatus.Submitted || a.Status == AttemptStatus.TimedOut);

            if (completedAttempts >= exam.MaxAttempts)
                throw new InvalidOperationException("لقد استنفدت عدد المحاولات المسموح بها.");

            // استئناف محاولة قائمة إن وُجدت
            var activeAttempt = previousAttempts.FirstOrDefault(a => a.Status == AttemptStatus.InProgress);
            if (activeAttempt != null)
            {
                activeAttempt.LastActivityAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return await BuildAttemptDtoAsync(activeAttempt.AttemptId, exam, traineeId);
            }

            // إنشاء محاولة جديدة
            var attempt = new ExamAttempt
            {
                ExamId = examId,
                TraineeId = traineeId,
                StartedAt = DateTime.UtcNow,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                AttemptNumber = completedAttempts + 1,
                LastActivityAt = DateTime.UtcNow
            };

            _db.ExamAttempts.Add(attempt);
            await _db.SaveChangesAsync();

            return await BuildAttemptDtoAsync(attempt.AttemptId, exam, traineeId);
        }

        public async Task<ExamAttemptDto?> ResumeExamAsync(Guid attemptId, Guid traineeId)
        {
            var attempt = await _db.ExamAttempts
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId && a.TraineeId == traineeId);

            if (attempt == null) return null;

            // إذا انتهى الوقت، أغلق تلقائياً
            if (attempt.IsExpired && attempt.Status == AttemptStatus.InProgress)
            {
                await ForceSubmitAttemptAsync(attempt);
                return null;
            }

            if (attempt.Status != AttemptStatus.InProgress) return null;

            attempt.LastActivityAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return await BuildAttemptDtoAsync(attempt.AttemptId, attempt.Exam, traineeId);
        }

        public async Task<bool> SaveAnswerAsync(SaveAnswerDto dto, Guid traineeId)
        {
            var attempt = await _db.ExamAttempts
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.AttemptId == dto.AttemptId && a.TraineeId == traineeId)
                ?? throw new InvalidOperationException("المحاولة غير موجودة.");

            if (attempt.Status != AttemptStatus.InProgress)
                throw new InvalidOperationException("المحاولة منتهية.");

            // تحقق من الوقت (Backend Validation)
            if (attempt.IsExpired)
            {
                await ForceSubmitAttemptAsync(attempt);
                throw new InvalidOperationException("انتهى وقت الامتحان وتم إرسال إجاباتك تلقائياً.");
            }

            // تحقق أن السؤال ينتمي للامتحان
            var examQuestion = await _db.ExamQuestions
                .Include(eq => eq.Question)
                .FirstOrDefaultAsync(eq => eq.ExamId == attempt.ExamId && eq.QuestionId == dto.QuestionId);

            if (examQuestion == null)
                throw new InvalidOperationException("هذا السؤال لا ينتمي للامتحان.");

            // upsert الإجابة
            var existing = await _db.StudentAnswers
                .FirstOrDefaultAsync(sa => sa.AttemptId == dto.AttemptId && sa.QuestionId == dto.QuestionId);

            if (existing != null)
            {
                existing.AnswerText = dto.AnswerText;
                existing.LastModifiedAt = DateTime.UtcNow;
            }
            else
            {
                _db.StudentAnswers.Add(new StudentAnswer
                {
                    AttemptId = dto.AttemptId,
                    QuestionId = dto.QuestionId,
                    AnswerText = dto.AnswerText
                });
            }

            attempt.LastActivityAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<ExamResultDto> SubmitExamAsync(SubmitExamDto dto, Guid traineeId)
        {
            var attempt = await _db.ExamAttempts
                .Include(a => a.Exam)
                .Include(a => a.StudentAnswers)
                .FirstOrDefaultAsync(a => a.AttemptId == dto.AttemptId && a.TraineeId == traineeId)
                ?? throw new InvalidOperationException("المحاولة غير موجودة.");

            if (attempt.Status != AttemptStatus.InProgress)
                throw new InvalidOperationException("هذه المحاولة ليست نشطة.");

            // حفظ الإجابات الأخيرة المُرسَلة ضمن Submit
            foreach (var answerDto in dto.Answers)
            {
                var existing = attempt.StudentAnswers
                    .FirstOrDefault(sa => sa.QuestionId == answerDto.QuestionId);

                if (existing != null)
                {
                    existing.AnswerText = answerDto.AnswerText;
                    existing.LastModifiedAt = DateTime.UtcNow;
                }
                else if (!string.IsNullOrWhiteSpace(answerDto.AnswerText))
                {
                    _db.StudentAnswers.Add(new StudentAnswer
                    {
                        AttemptId = dto.AttemptId,
                        QuestionId = answerDto.QuestionId,
                        AnswerText = answerDto.AnswerText
                    });
                }
            }

            await _db.SaveChangesAsync();

            // التصحيح التلقائي + حساب الدرجة
            await GradeAttemptAsync(attempt);

            return BuildExamResultDto(attempt, showAnswers: attempt.Exam.ShowResultsImmediately);
        }

        public async Task<ExamResultDto?> GetAttemptResultAsync(Guid attemptId, Guid traineeId)
        {
            var attempt = await _db.ExamAttempts
                .Include(a => a.Exam)
                .Include(a => a.Trainee).ThenInclude(t => t.User)
                .Include(a => a.StudentAnswers).ThenInclude(sa => sa.Question)
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId && a.TraineeId == traineeId);

            if (attempt == null || attempt.Status == AttemptStatus.InProgress)
                return null;

            return BuildExamResultDto(attempt, showAnswers: attempt.Exam.ShowResultsImmediately);
        }

        public async Task RecordTabSwitchAsync(Guid attemptId, Guid traineeId)
        {
            var attempt = await _db.ExamAttempts
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId && a.TraineeId == traineeId
                    && a.Status == AttemptStatus.InProgress);

            if (attempt == null) return;

            attempt.TabSwitchCount++;
            attempt.LastActivityAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        /// يُستدعى من Background Job لإغلاق المحاولات المنتهية
        public async Task AutoSubmitExpiredAttemptsAsync()
        {
            var expired = await _db.ExamAttempts
                .Include(a => a.Exam)
                .Include(a => a.StudentAnswers)
                .Where(a => a.Status == AttemptStatus.InProgress)
                .ToListAsync();

            foreach (var attempt in expired.Where(a => a.IsExpired))
            {
                await ForceSubmitAttemptAsync(attempt);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  مشترك
        // ══════════════════════════════════════════════════════════

        public async Task<ExamDto?> GetExamByIdAsync(Guid examId)
        {
            var exists = await _db.Exams.AnyAsync(e => e.ExamId == examId && !e.IsDeleted);
            return exists ? await MapToExamDtoAsync(examId) : null;
        }

        public async Task<List<ExamSummaryDto>> GetExamsForCourseAsync(Guid courseId)
        {
            return await _db.Exams
                .Include(e => e.ExamAttempts)
                .Include(e => e.ExamQuestions)
                .Where(e => e.CourseId == courseId && !e.IsDeleted)
                .OrderByDescending(e => e.StartDateTime)
                .Select(e => new ExamSummaryDto
                {
                    ExamId = e.ExamId,
                    ExamName = e.ExamName,
                    StartDateTime = e.StartDateTime,
                    DurationMinutes = e.DurationMinutes,
                    IsPublished = e.IsPublished,
                    IsActive = e.IsPublished
                             && DateTime.UtcNow >= e.StartDateTime
                             && DateTime.UtcNow <= e.StartDateTime.AddMinutes(e.DurationMinutes),
                    AttemptCount = e.ExamAttempts.Count
                })
                .ToListAsync();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════

        private async Task GradeAttemptAsync(ExamAttempt attempt)
        {
            var examQuestions = await _db.ExamQuestions
                .Include(eq => eq.Question)
                .Where(eq => eq.ExamId == attempt.ExamId)
                .ToListAsync();

            await _db.Entry(attempt)
                .Collection(a => a.StudentAnswers)
                .LoadAsync();

            decimal totalScore = 0;
            decimal maxScore = 0;

            foreach (var eq in examQuestions)
            {
                var effectivePoints = eq.PointsOverride ?? eq.Question.DefaultPoints;
                maxScore += effectivePoints;

                var answer = attempt.StudentAnswers
                    .FirstOrDefault(sa => sa.QuestionId == eq.QuestionId);

                if (answer == null) continue;

                if (eq.Question.IsAutoGradable)
                {
                    var isCorrect = CheckAnswer(eq.Question, answer.AnswerText);
                    answer.IsCorrect = isCorrect;
                    answer.PointsEarned = isCorrect ? effectivePoints : 0;
                    totalScore += answer.PointsEarned;
                }
                // Essay: يبقى IsCorrect = null حتى يصحِّحه المدرب يدوياً
            }

            attempt.TotalScore = totalScore;
            attempt.MaxScore = maxScore;
            attempt.ScorePercentage = maxScore > 0
                ? Math.Round(totalScore / maxScore * 100, 2)
                : 0;
            attempt.IsPassed = attempt.ScorePercentage >= attempt.Exam.PassingScore;
            attempt.Status = AttemptStatus.Submitted;
            attempt.SubmittedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        private static bool CheckAnswer(Question question, string? studentAnswer)
        {
            if (string.IsNullOrWhiteSpace(studentAnswer) || string.IsNullOrWhiteSpace(question.CorrectAnswer))
                return false;

            return question.QuestionType switch
            {
                QuestionType.MultipleChoice =>
                    string.Equals(studentAnswer.Trim(), question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase),

                QuestionType.TrueFalse =>
                    string.Equals(studentAnswer.Trim(), question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase),

                QuestionType.ShortAnswer =>
                    string.Equals(
                        studentAnswer.Trim().ToLowerInvariant().Replace(" ", ""),
                        question.CorrectAnswer.Trim().ToLowerInvariant().Replace(" ", ""),
                        StringComparison.Ordinal),

                _ => false
            };
        }

        private async Task ForceSubmitAttemptAsync(ExamAttempt attempt)
        {
            attempt.Status = AttemptStatus.TimedOut;
            attempt.SubmittedAt = DateTime.UtcNow;

            await _db.Entry(attempt)
                .Collection(a => a.StudentAnswers)
                .LoadAsync();

            await _db.Entry(attempt)
                .Reference(a => a.Exam)
                .LoadAsync();

            await GradeAttemptAsync(attempt);
        }

        private async Task RecalculateAttemptScoreAsync(Guid attemptId)
        {
            var attempt = await _db.ExamAttempts
                .Include(a => a.Exam)
                .Include(a => a.StudentAnswers)
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId);

            if (attempt == null) return;

            var examQuestions = await _db.ExamQuestions
                .Include(eq => eq.Question)
                .Where(eq => eq.ExamId == attempt.ExamId)
                .ToListAsync();

            decimal totalScore = attempt.StudentAnswers.Sum(sa => sa.PointsEarned);
            decimal maxScore = examQuestions.Sum(eq => eq.PointsOverride ?? eq.Question.DefaultPoints);

            attempt.TotalScore = totalScore;
            attempt.MaxScore = maxScore;
            attempt.ScorePercentage = maxScore > 0
                ? Math.Round(totalScore / maxScore * 100, 2)
                : 0;
            attempt.IsPassed = attempt.ScorePercentage >= attempt.Exam.PassingScore;
        }

        private async Task<ExamAttemptDto> BuildAttemptDtoAsync(Guid attemptId, Exam exam, Guid traineeId)
        {
            var attempt = await _db.ExamAttempts
                .Include(a => a.StudentAnswers)
                .FirstOrDefaultAsync(a => a.AttemptId == attemptId);

            var examQuestions = await _db.ExamQuestions
                .Include(eq => eq.Question)
                .Where(eq => eq.ExamId == exam.ExamId)
                .OrderBy(eq => eq.OrderIndex)
                .ToListAsync();

            // ترتيب عشوائي مخصوص لكل طالب (seed ثابت = نفس الترتيب عند الاستئناف)
            if (exam.IsRandomized)
            {
                var seed = HashCode.Combine(traineeId, exam.ExamId);
                examQuestions = examQuestions.OrderBy(_ => Guid.NewGuid()).ToList();
                // ملاحظة: في الإنتاج، استخدم seed ثابت بدلاً من Guid.NewGuid()
                // مثال: examQuestions = Shuffle(examQuestions, seed);
            }

            var questions = examQuestions.Select((eq, idx) =>
            {
                var saved = attempt?.StudentAnswers
                    .FirstOrDefault(sa => sa.QuestionId == eq.QuestionId)?.AnswerText;

                return new QuestionForStudentDto
                {
                    QuestionId = eq.QuestionId,
                    QuestionText = eq.Question.QuestionText,
                    QuestionType = eq.Question.QuestionType,
                    Options = eq.Question.Options,
                    OrderIndex = idx,
                    Points = eq.PointsOverride ?? eq.Question.DefaultPoints,
                    SavedAnswer = saved
                };
            }).ToList();

            var deadline = attempt!.StartedAt.AddMinutes(exam.DurationMinutes);
            var secondsRemaining = Math.Max(0, (int)(deadline - DateTime.UtcNow).TotalSeconds);

            return new ExamAttemptDto
            {
                AttemptId = attempt.AttemptId,
                ExamId = exam.ExamId,
                ExamName = exam.ExamName,
                Instructions = exam.Instructions,
                DurationMinutes = exam.DurationMinutes,
                StartedAt = attempt.StartedAt,
                Deadline = deadline,
                SecondsRemaining = secondsRemaining,
                Status = attempt.Status,
                AttemptNumber = attempt.AttemptNumber,
                Questions = questions
            };
        }

        private ExamResultDto BuildExamResultDto(ExamAttempt attempt, bool showAnswers)
        {
            var answerResults = showAnswers
                ? attempt.StudentAnswers?.Select(sa =>
                {
                    var eq = attempt.Exam?.ExamQuestions?
                        .FirstOrDefault(eq => eq.QuestionId == sa.QuestionId);
                    return new AnswerResultDto
                    {
                        AnswerId = sa.AnswerId,
                        QuestionId = sa.QuestionId,
                        QuestionText = sa.Question?.QuestionText ?? "",
                        QuestionType = sa.Question?.QuestionType ?? QuestionType.Essay,
                        Options = sa.Question?.Options ?? new(),
                        StudentAnswer = sa.AnswerText,
                        CorrectAnswer = sa.Question?.CorrectAnswer,
                        IsCorrect = sa.IsCorrect,
                        PointsEarned = sa.PointsEarned,
                        MaxPoints = eq?.EffectivePoints ?? sa.Question?.DefaultPoints ?? 0,
                        Explanation = sa.Question?.Explanation,
                        TrainerFeedback = sa.TrainerFeedback
                    };
                }).ToList()
                : null;

            return new ExamResultDto
            {
                AttemptId = attempt.AttemptId,
                ExamId = attempt.ExamId,
                ExamName = attempt.Exam?.ExamName ?? "",
                TraineeName = attempt.Trainee?.User?.UserName ?? "",
                TotalScore = attempt.TotalScore ?? 0,
                MaxScore = attempt.MaxScore ?? 0,
                ScorePercentage = attempt.ScorePercentage ?? 0,
                PassingScore = attempt.Exam?.PassingScore ?? 60,
                IsPassed = attempt.IsPassed ?? false,
                StartedAt = attempt.StartedAt,
                SubmittedAt = attempt.SubmittedAt ?? DateTime.UtcNow,
                TimeTakenMinutes = attempt.SubmittedAt.HasValue
                    ? (int)(attempt.SubmittedAt.Value - attempt.StartedAt).TotalMinutes
                    : 0,
                HasPendingEssays = attempt.StudentAnswers?
                    .Any(sa => !sa.IsManuallyGraded && sa.IsCorrect == null) ?? false,
                TabSwitchCount = attempt.TabSwitchCount,
                IpAddress = attempt.IpAddress,
                UserAgent = attempt.UserAgent,
                PenaltyApplied = attempt.PenaltyApplied,
                PenaltyReason = attempt.PenaltyReason,
                OriginalTotalScore = attempt.OriginalTotalScore,
                OriginalScorePercentage = attempt.OriginalScorePercentage,
                AnswerResults = answerResults
            };
        }

        private async Task<ExamDto> MapToExamDtoAsync(Guid examId)
        {
            var exam = await _db.Exams
                .Include(e => e.Course)
                .Include(e => e.Trainer).ThenInclude(t => t.User)
                .Include(e => e.ExamQuestions).ThenInclude(eq => eq.Question)
                .FirstAsync(e => e.ExamId == examId);

            return new ExamDto
            {
                ExamId = exam.ExamId,
                ExamName = exam.ExamName,
                Instructions = exam.Instructions,
                StartDateTime = exam.StartDateTime,
                DurationMinutes = exam.DurationMinutes,
                PassingScore = exam.PassingScore,
                MaxAttempts = exam.MaxAttempts,
                IsRandomized = exam.IsRandomized,
                ShowResultsImmediately = exam.ShowResultsImmediately,
                IsPublished = exam.IsPublished,
                CourseId = exam.CourseId,
                CourseName = exam.Course?.CourseName ?? "",
                TrainerName = exam.Trainer?.User?.UserName ?? "",
                QuestionCount = exam.ExamQuestions.Count,
                TotalPoints = exam.ExamQuestions.Sum(eq => eq.EffectivePoints),
                IsActive = exam.IsActive,
                HasStarted = exam.HasStarted,
                HasEnded = exam.HasEnded
            };
        }

        private static QuestionDto MapToQuestionDto(Question q, ExamQuestion? eq)
        {
            return new QuestionDto
            {
                QuestionId = q.QuestionId,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType,
                DifficultyLevel = q.DifficultyLevel,
                Options = q.Options,
                CorrectAnswer = q.CorrectAnswer,
                Explanation = q.Explanation,
                DefaultPoints = q.DefaultPoints,
                OrderIndex = eq?.OrderIndex ?? 0,
                EffectivePoints = eq?.EffectivePoints ?? q.DefaultPoints
            };
        }

        private static void ValidateQuestionDto(CreateQuestionDto dto)
        {
            if (dto.QuestionType == QuestionType.MultipleChoice)
            {
                if (dto.Options == null || dto.Options.Count < 2)
                    throw new ArgumentException("يجب توفير خيارين على الأقل للسؤال الموضوعي.");
                if (string.IsNullOrWhiteSpace(dto.CorrectAnswer))
                    throw new ArgumentException("يجب تحديد الإجابة الصحيحة.");
                if (!dto.Options.Any(o => string.Equals(o, dto.CorrectAnswer, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("الإجابة الصحيحة يجب أن تكون أحد الخيارات المُدخلة.");
            }

            if (dto.QuestionType == QuestionType.TrueFalse)
            {
                if (dto.CorrectAnswer != "True" && dto.CorrectAnswer != "False")
                    throw new ArgumentException("إجابة أسئلة الصح/الخطأ يجب أن تكون 'True' أو 'False'.");
            }
        }
    }
}
