namespace TrainingCenterManagement_MVC.ViewModels
{
    public class ReceptionistDashboardViewModel
    {
        public string ReceptionistName { get; set; } = string.Empty;
        public string ReceptionistEmail { get; set; } = string.Empty;
        public string ProfilePic { get; set; } = "/images/default-profile.png";

        public ReceptionistStats Stats { get; set; } = new();
        public List<RecentPaymentEntry> RecentPayments { get; set; } = new();
        public List<TodayEnrollment> TodayEnrollments { get; set; } = new();
        public List<CourseQuickInfo> ActiveCourses { get; set; } = new();
    }

    public class ReceptionistStats
    {
        public int TotalStudents { get; set; }
        public int StudentsChange { get; set; }
        public int TotalCourses { get; set; }
        public int CoursesChange { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal RevenueChange { get; set; }
        public int TodayPayments { get; set; }
        public int PendingEnrollments { get; set; }
    }

    public class RecentPaymentEntry
    {
        public Guid PaymentId { get; set; }
        public string TraineeName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "ل.س";
        public string? Notes { get; set; }
        public DateTime Date { get; set; }
    }

    public class TodayEnrollment
    {
        public string TraineeName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public DateTime EnrolledAt { get; set; }
    }

    public class CourseQuickInfo
    {
        public Guid CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public int EnrolledCount { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
    }
}
