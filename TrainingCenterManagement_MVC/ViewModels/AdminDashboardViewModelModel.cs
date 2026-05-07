namespace TrainingCenterManagement_MVC.ViewModels
{
    public class AdminDashboardViewModelModel
    {
        // Admin identity
        public string AdminName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminProfilePic { get; set; } = "/images/default-profile.png";

        // Core KPIs
        public Stats Stats { get; set; } = new();
        public Dictionary<string, ChartData> ChartData { get; set; } = new();

        // Extended platform health
        public PlatformHealth Health { get; set; } = new();

        // Rich data sections
        public List<RecentActivityItem> RecentActivity { get; set; } = new();
        public List<TopCourseItem> TopCourses { get; set; } = new();
        public List<RecentPaymentItem> RecentPayments { get; set; } = new();
        public List<TopTrainerItem> TopTrainers { get; set; } = new();
    }

    // ─── Core Stats ──────────────────────────────────────────────────────────────
    public class Stats
    {
        public int TotalCourses { get; set; }
        public int CoursesChange { get; set; }
        public int ActiveStudents { get; set; }
        public int StudentsChange { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal RevenueChange { get; set; }
        public int ActiveInstitutes { get; set; }
        public int InstitutesChange { get; set; }
    }

    public class ChartData
    {
        public List<string> Labels { get; set; } = new();
        public List<decimal> Values { get; set; } = new();
    }

    // ─── Platform Health ─────────────────────────────────────────────────────────
    public class PlatformHealth
    {
        public int TotalCertificates { get; set; }
        public int TotalLectures { get; set; }
        public int TotalExams { get; set; }
        public int TotalPresences { get; set; }
    }

    // ─── Recent Activity ─────────────────────────────────────────────────────────
    public class RecentActivityItem
    {
        public string Type { get; set; } = string.Empty;   // enrollment | payment | course | trainer
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public string Icon { get; set; } = "fas fa-circle";
        public string Color { get; set; } = "#6366f1";
    }

    // ─── Top Courses ─────────────────────────────────────────────────────────────
    public class TopCourseItem
    {
        public Guid CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public int EnrollmentCount { get; set; }
        public decimal AverageRating { get; set; }
        public int LectureCount { get; set; }
        public decimal Price { get; set; }
    }

    // ─── Recent Payments ─────────────────────────────────────────────────────────
    public class RecentPaymentItem
    {
        public string TraineeName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }

    // ─── Top Trainers ────────────────────────────────────────────────────────────
    public class TopTrainerItem
    {
        public Guid TrainerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string ProfilePic { get; set; } = string.Empty;
        public int StudentCount { get; set; }
        public int CourseCount { get; set; }
    }
}
