using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.DTOs;
using TrainingCenterManagement_MVC.Models.Enums;

namespace TrainingCenterManagement_MVC.Services
{
    public interface IExamService
    {
        // ── Trainer: إدارة الامتحانات ──────────────────────────────

        Task<ExamDto> CreateExamAsync(CreateExamDto dto, Guid trainerId);
        Task<ExamDto> UpdateExamAsync(UpdateExamDto dto, Guid trainerId);
        Task<bool> DeleteExamAsync(Guid examId, Guid trainerId);
        Task<bool> PublishExamAsync(Guid examId, Guid trainerId);
        Task<bool> UnpublishExamAsync(Guid examId, Guid trainerId);

        // ── Trainer: بنك الأسئلة ───────────────────────────────────

        Task<QuestionDto> CreateQuestionAsync(CreateQuestionDto dto, Guid trainerId);
        Task<QuestionDto> UpdateQuestionAsync(UpdateQuestionDto dto, Guid trainerId);
        Task<bool> DeleteQuestionAsync(Guid questionId, Guid trainerId);
        Task<List<QuestionDto>> GetQuestionBankAsync(Guid trainerId, QuestionType? type = null, DifficultyLevel? difficulty = null);

        // ── Trainer: إدارة أسئلة الامتحان ─────────────────────────

        Task AddQuestionToExamAsync(AddQuestionToExamDto dto, Guid trainerId);
        Task RemoveQuestionFromExamAsync(Guid examId, Guid questionId, Guid trainerId);
        Task ReorderQuestionsAsync(Guid examId, List<(Guid QuestionId, int OrderIndex)> order, Guid trainerId);

        // ── Trainer: نتائج الطلاب ──────────────────────────────────

        Task<TrainerExamResultsDto> GetExamResultsForTrainerAsync(Guid examId, Guid trainerId);
        Task<ExamResultDto> GetAttemptDetailForTrainerAsync(Guid attemptId, Guid trainerId);
        Task<bool> ManualGradeAnswerAsync(ManualGradeDto dto, Guid trainerId);
        Task<bool> ApplyPenaltyAsync(ApplyPenaltyDto dto, Guid trainerId);
        Task<bool> RemovePenaltyAsync(Guid attemptId, Guid trainerId);

        // ── Trainee: أداء الامتحان ─────────────────────────────────

        Task<List<ExamSummaryDto>> GetAvailableExamsForTraineeAsync(Guid traineeId);
        Task<ExamAttemptDto> StartExamAsync(Guid examId, Guid traineeId, string ipAddress, string userAgent);
        Task<ExamAttemptDto?> ResumeExamAsync(Guid attemptId, Guid traineeId);
        Task<bool> SaveAnswerAsync(SaveAnswerDto dto, Guid traineeId);
        Task<ExamResultDto> SubmitExamAsync(SubmitExamDto dto, Guid traineeId);
        Task<ExamResultDto?> GetAttemptResultAsync(Guid attemptId, Guid traineeId);
        Task RecordTabSwitchAsync(Guid attemptId, Guid traineeId);
        Task AutoSubmitExpiredAttemptsAsync();

        // ── مشترك ──────────────────────────────────────────────────

        Task<ExamDto?> GetExamByIdAsync(Guid examId);
        Task<List<ExamSummaryDto>> GetExamsForCourseAsync(Guid courseId);
    }
}
