using System;
using System.Collections.Generic;

namespace TrainingCenterManagement_MVC.ViewModels
{
    public class TrainerDashboardViewModel
    {
        public string FullName { get; set; }
        public string ProfilePictureUrl { get; set; } = "/images/default-profile.png";
        public string Specialization { get; set; } = "General Trainer";
        public string Availability { get; set; } = "Available"; // "Available" or "Busy"
        public string WelcomeMessage { get; set; }
        public int OverallProgress { get; set; } // e.g., Average Attendance %

        public TrainerDashboardStats Stats { get; set; } = new TrainerDashboardStats();
        public List<CurrentCourse> CurrentCourses { get; set; } = new List<CurrentCourse>();
        public List<RecommendedCourse> RecommendedCourses { get; set; } = new List<RecommendedCourse>();
        public List<UpcomingEvent> UpcomingEvents { get; set; } = new List<UpcomingEvent>();
        public List<CertificateViewModel> Certificates { get; set; } = new List<CertificateViewModel>();
        public DashboardChartData ChartData { get; set; } = new DashboardChartData();

        // New for At-a-Glance
        public TrainerKPIs KPIs { get; set; } = new TrainerKPIs();
        public List<Notification> Notifications { get; set; } = new List<Notification>();

        // For Schedule
        public List<ScheduleEvent> ScheduleEvents { get; set; } = new List<ScheduleEvent>();

        // For Students Management
        public Dictionary<Guid, List<StudentInfo>> StudentsByCourse { get; set; } = new Dictionary<Guid, List<StudentInfo>>();

        // For Assignments
        public List<Assignment> Assignments { get; set; } = new List<Assignment>();

        // For Attendance
        public List<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

        // For Analytics
        public List<Report> Reports { get; set; } = new List<Report>();

        // For Resources
        public List<Resource> Resources { get; set; } = new List<Resource>();

    }

    public class TrainerKPIs
    {
        public int CurrentCourses { get; set; }
        public int EnrolledStudents { get; set; }
        public decimal AverageAttendance { get; set; } // Last week/month
        public int UngradedAssignments { get; set; }
        public decimal AverageRating { get; set; }
    }
    public class TrainerDashboardStats
    {
       
        public int TotalProgress { get; set; }
        public int CurrentCourses { get; set; }
        public int Certificates { get; set; }
        public int UpcomingEvents { get; set; }
        
    }
    public class Notification
    {
        public string Message { get; set; }
        public DateTime Time { get; set; }
    }

    public class ScheduleEvent
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Type { get; set; } // Lecture, Exam, Meeting
        public string Link { get; set; }
    }

    public class StudentInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string AttendanceStatus { get; set; }
        public decimal Grade { get; set; }
        public string Contact { get; set; }
        public string CourseName { get; set; }
    }

    public class Assignment
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public DateTime DueDate { get; set; }
        public string Type { get; set; } // Quiz, Project, Homework
    }

    public class AttendanceRecord
    {
        public Guid LectureId { get; set; }
        public DateTime Date { get; set; }
        public List<StudentAttendance> Students { get; set; } = new List<StudentAttendance>();
    }

    public class StudentAttendance
    {
        public Guid StudentId { get; set; }
        public bool Present { get; set; }
        public string Notes { get; set; }
    }

    public class Report
    {
        public string Title { get; set; }
        public string Data { get; set; } // JSON or something for chart/report
    }

    public class Resource
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // PDF, Video, etc.
        public string Url { get; set; }
    }

    // Reuse other classes as needed
}