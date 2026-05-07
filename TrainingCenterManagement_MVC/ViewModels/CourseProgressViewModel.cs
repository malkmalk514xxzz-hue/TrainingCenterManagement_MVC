using System;
using System.Collections.Generic;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class CourseProgressViewModel
    {
        public Guid CourseId { get; set; }
        public string CourseName { get; set; }
        public int TotalLectures { get; set; }
        public int AttendedLectures { get; set; }
        public int AttendancePercent => TotalLectures == 0 ? 0 : (int)Math.Round(AttendedLectures * 100.0 / TotalLectures);
        public int TotalVideos { get; set; }
        public int WatchedVideos { get; set; }
        public int VideoPercent => TotalVideos == 0 ? 0 : (int)Math.Round(WatchedVideos * 100.0 / TotalVideos);
        public bool HasCertificate { get; set; }
        public int OverallPercent => CalculateOverall();
        public List<LectureProgressItem> Lectures { get; set; } = new();
        public List<ExamProgressItem> Exams { get; set; } = new();

        private int CalculateOverall()
        {
            int parts = 0, total = 0;
            if (TotalLectures > 0) { total += AttendancePercent; parts++; }
            if (TotalVideos > 0) { total += VideoPercent; parts++; }
            if (Exams.Count > 0)
            {
                var best = Exams.Where(e => e.BestScorePercentage.HasValue)
                                .Select(e => e.BestScorePercentage!.Value).DefaultIfEmpty(0).Max();
                total += (int)best; parts++;
            }
            return parts == 0 ? 0 : total / parts;
        }
    }

    public class LectureProgressItem
    {
        public Guid LectureId { get; set; }
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public bool IsAttended { get; set; }
        public int TotalVideos { get; set; }
        public int WatchedVideos { get; set; }
        public int TotalMaterials { get; set; }
    }

    public class ExamProgressItem
    {
        public Guid ExamId { get; set; }
        public string ExamTitle { get; set; }
        public decimal? BestScorePercentage { get; set; }
        public decimal? BestTotalScore { get; set; }
        public decimal? MaxScore { get; set; }
        public bool HasAttempt => BestScorePercentage.HasValue;
        public DateTime? AttemptDate { get; set; }
    }
}
