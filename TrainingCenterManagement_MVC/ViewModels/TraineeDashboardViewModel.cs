using System;
using System.Collections.Generic;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class TraineeDashboardViewModel
    {
        public string FullName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string ProfilePictureUrl { get; set; }
        public string WelcomeMessage { get; set; }
        public int OverallProgress { get; set; }
        public DashboardStats Stats { get; set; }
        public DashboardChartData ChartData { get; set; }
        public List<CurrentCourse> CurrentCourses { get; set; } = new List<CurrentCourse>();
        public List<RecommendedCourse> RecommendedCourses { get; set; } = new List<RecommendedCourse>();
        public List<UpcomingEvent> UpcomingEvents { get; set; } = new List<UpcomingEvent>();
        public List<CertificateViewModel> Certificates { get; set; } = new List<CertificateViewModel>();
        public Guid TraineeId { get; set; }
        public List<Notification> Notifications { get; set; } = new List<Notification>();
        public List<LiveSessionSummary> UpcomingLiveSessions { get; set; } = new List<LiveSessionSummary>();

        // Wallet
        public decimal BalanceUSD { get; set; }
        public decimal BalanceSYP { get; set; }
        public decimal TotalEquivalentUSD { get; set; }
        public string TransferCode { get; set; } = string.Empty;

        // Payment history split by source
        public List<PaymentHistoryItem> ShamCashPayments { get; set; } = new();
        public List<PaymentHistoryItem> AdminPayments { get; set; } = new();
        public decimal TotalShamCashSYP { get; set; }
        public decimal TotalAdminSYP { get; set; }
    }

    public class DashboardStats
    {
        public int OverallProgress { get; set; }
        public int EnrolledCourses { get; set; }
        public int CompletedHours { get; set; }
        public int CompletedExams { get; set; }
        public int AttendanceRate { get; set; }
    }

    public class DashboardChartData
    {
        public ChartData Analytics { get; set; }
    }

   

    public class CurrentCourse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Progress { get; set; }
        public string Remaining { get; set; }
    }

    public class RecommendedCourse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";
        public string? ThumbnailUrl { get; set; }
        public int LectureCount { get; set; }
    }

    public class UpcomingEvent
    {
        public string Title { get; set; }
        public string Date { get; set; }
        public string Link { get; set; }
    }

    public class CertificateViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class LiveSessionSummary
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public DateTime ScheduledAt { get; set; }
        public bool IsLiveNow { get; set; }
    }

    public class PaymentHistoryItem
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "SYP";
        public string CourseName { get; set; } = "";
        public string Notes { get; set; } = "";
        public DateTime Date { get; set; }
    }
}