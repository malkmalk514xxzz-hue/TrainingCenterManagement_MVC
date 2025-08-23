using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize] // Este atributo garante que o utilizador deve estar autenticado
    public class DashboardController : Controller
    {
        
        private readonly ApplicationDbContext context;
        private readonly DashboardHelper _dashboardHelper;

        public DashboardController( ApplicationDbContext context ,
           DashboardHelper dashboardHelper)
        {
            
            this.context = context;
            _dashboardHelper = dashboardHelper;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }
         
        [Authorize(Roles = "Admin")] 
        public async Task<IActionResult> AdminDashboard()
        {
            var model = new AdminDashboardViewModelModel
            {
                Stats = _dashboardHelper.GetDashboardStats(),
                ChartData = _dashboardHelper.GetChartData()

            };
            Console.WriteLine($"Stats: TotalCourses={model.Stats.TotalCourses}, CoursesChange={model.Stats.CoursesChange}, ActiveStudents={model.Stats.ActiveStudents}");
            Console.WriteLine($"ChartData: Overview={string.Join(",", model.ChartData["overview"].Values)}");


            return View(model);


        }



        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> TraineeDashboard()
        {
            var userid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var traineeId = context.Trainees.FirstOrDefault(t=>t.UserId==userid).TraineeId;
            if (traineeId == null || string.IsNullOrEmpty(userid) )
            {
                return Unauthorized(); // Or handle appropriately if user ID is not found
            }

            
            var model = await _dashboardHelper.GetDashboardDataAsync(traineeId,userid);
            model.TraineeId = traineeId;
            return View(model);
        }
        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> TrainerDashboard()
        {
            // Get the current user's ID from the authenticated user's claims
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Fetch the trainer associated with the user
            var trainer = await context.Trainers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (trainer == null)
            {
                return NotFound("Trainer profile not found.");
            }

            // Fetch the full name from the ApplicationUser
            var user = await context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound("User not found.");
            }

            // Use DashboardHelper to get the complete dashboard data
            var viewModel = await _dashboardHelper.GetTrainerDashboardAsync(trainer.TrainerId, user.FullName);

            return View(viewModel);
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
