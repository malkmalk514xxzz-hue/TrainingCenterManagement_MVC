using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
            // التحقق مما إذا كان المستخدم مصادقًا
            if (User.Identity.IsAuthenticated)
            {
                
                // إذا كان المستخدم مدربًا، إعادة توجيه إلى لوحة تحكم المدرب
                if (User.IsInRole("Trainer"))
                {
                    return RedirectToAction("TrainerDashboard", "Dashboard");
                }
                // إذا كان المستخدم طالبًا، إعادة توجيه إلى لوحة تحكم الطالب
                else if (User.IsInRole("Trainee"))
                {
                    return RedirectToAction("TraineeDashboard", "Dashboard");
                }
                // إذا كان المستخدم مديرًا، إعادة توجيه إلى لوحة تحكم المدير
                else if (User.IsInRole("Admin"))
                {
                    return RedirectToAction("AdminDashboard", "Dashboard");
                }
                else if (User.IsInRole("Receptionist"))
                {
                    return RedirectToAction("ReceptionistDashboard", "Dashboard");
                }
                // إذا كان للمستخدم دور آخر أو لا ينتمي إلى أي من الأدوار المحددة
                else
                {
                    // إعادة توجيه إلى صفحة افتراضية أو عرض رسالة خطأ
                    return RedirectToAction("Error", "Home", new { message = "دور المستخدم غير مدعوم." });
                }
            }
            else
            {
                // إذا لم يكن المستخدم مصادقًا، إعادة توجيه إلى صفحة تسجيل الدخول
                return RedirectToAction("Login", "Account");
            }
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
            try
            {
                var viewModel = await _dashboardHelper.GetTrainerDashboardAsync(trainer.TrainerId, user.FullName);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                // تسجيل الخطأ (logging) 
                return StatusCode(500, "An error occurred while loading the dashboard.");
            }
        }


        [Authorize(Roles = "Receptionist")]
        public async Task<IActionResult> ReceptionistDashboard()
        {
            var helper = new DashboardHelper(context);
            ViewBag.ActiveStudents = helper.GetActiveStudents();
            ViewBag.MonthlyRevenue = helper.GetMonthlyRevenue();
            ViewBag.TotalCourses = helper.GetTotalCourses();
            ViewData["CourseId"] = new SelectList(context.Courses, "CourseId", "CourseName");
            ViewData["TraineeId"] = new SelectList(context.Trainees.Include(t => t.User), "TraineeId", "User.FullName");
            return View();
        }

    }
}
