using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainingCenterManagement_MVC.Data;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize] // Este atributo garante que o utilizador deve estar autenticado
    public class DashboardController : Controller
    {
        
        private readonly ApplicationDbContext context;

        public DashboardController( ApplicationDbContext context)
        {
            
            this.context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }
         
        [Authorize(Roles = "Admin")] 
        public async Task<IActionResult> AdminDashboard()
        {
            //int totalusers = context.Users.Count();
            //int totalteachers = context.Teachers.Count();
            //var totalstudents = await userHelper.GetAllUsersInRoleAsync("Student");
            //int totalstudentscount = totalstudents.Count();
            //int totalcourses = context.Courses.Count();
            //int totalsubjects = context.Subjects.Count();
            //AdminDashboardViewModel viewModel = new AdminDashboardViewModel()
            //{
            //    TotalCourses = totalcourses,
            //    TotalTeachers = totalteachers,
            //    TotalSubjects = totalsubjects,
            //    TotalUsers = totalusers,
            //    TotalStudents = totalstudentscount,

            //};
            return View(
                //viewModel
                );
        }

       
        
        [Authorize(Roles = "Trainee")] 
        public IActionResult TraineeDashboard()
        {
    //        var userid = HttpContext.Session.GetString("UserId");
    //        int totalstudentsInTeacherCourse = context.Teachers
    //.Where(t => t.UserId == userid)
    //.SelectMany(t => t.TeacherClasses)                  // الكورسات التي يدرسها المعلم
    //.Select(c => c.Class)                               // الكورس نفسه
    //.SelectMany(c => c.Students)                         // الطلاب المسجلين في الكورس
    //.Count();


    //        int totalcourses = context.Teachers
    //            .Where (t => t.UserId == userid)
    //            .SelectMany(t => t.TeacherClasses)// الكورسات التي يدرسها المعلم
    //                      .Count();
           
            //TeacherDashboardViewModel viewModel = new TeacherDashboardViewModel()
            //{
            //    TotalStudentsInTeacherCourses = totalstudentsInTeacherCourse,
            //    TotalCourses = totalcourses

            //};
            return View(
               // viewModel
                );
        }

        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> TrainerDashboard()
        {
            // var userid = HttpContext.Session.GetString("UserId");

            // //var user = await userHelper.GetUserByIdAsync(userid);
            //// var user2 = await context.Students.FirstOrDefaultAsync(s=>s.UserId == userid);
            // int totalstudentcourses = context.Users.Where(u => u.Id==userid).Select(u=>u.Courses).Count();

            //StudentDashboardViewModel viewModel = new StudentDashboardViewModel()
            //{
            //   TotalStudentCourses = totalstudentcourses,

            //};
            return View(
                //viewModel
                );
        }
        
        [Authorize(Roles = "Receptionist")]
        public async Task<IActionResult> ReceptionistDashboard()
        {
            // var userid = HttpContext.Session.GetString("UserId");

            // //var user = await userHelper.GetUserByIdAsync(userid);
            //// var user2 = await context.Students.FirstOrDefaultAsync(s=>s.UserId == userid);
            // int totalstudentcourses = context.Users.Where(u => u.Id==userid).Select(u=>u.Courses).Count();

            //StudentDashboardViewModel viewModel = new StudentDashboardViewModel()
            //{
            //   TotalStudentCourses = totalstudentcourses,

            //};
            return View(
                //viewModel
                );
        }
    }
}
