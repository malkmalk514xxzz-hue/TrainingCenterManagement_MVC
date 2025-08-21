
namespace TrainingCenterManagement_MVC.ViewModels
{
    public class AdminDashboardViewModelModel
    {
        public Stats Stats { get; set; }
        public Dictionary<string, ChartData> ChartData { get; set; }
    }

    public class Stats
    {
        public int TotalCourses { get; set; }
        //لتغيير في عدد الدورات التدريبية (Courses) خلال   الشهر الحالي
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
        public List<string> Labels { get; set; }
        public List<decimal> Values { get; set; }
    }
}
