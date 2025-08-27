using System;
using System.Collections.Generic;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class TraineeDashboardViewModel
    {

        public string FullName { get; set; }
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
}