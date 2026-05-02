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
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly DashboardHelper _dashboardHelper;

        public DashboardController(ApplicationDbContext context, DashboardHelper dashboardHelper)
        {
            _context = context;
            _dashboardHelper = dashboardHelper;
        }

        public async Task<IActionResult> Index()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Account");

            if (User.IsInRole("Trainer"))
                return RedirectToAction("TrainerDashboard", "Dashboard");

            if (User.IsInRole("Trainee"))
                return RedirectToAction("TraineeDashboard", "Dashboard");

            if (User.IsInRole("Admin"))
                return RedirectToAction("AdminDashboard", "Dashboard");

            if (User.IsInRole("Receptionist"))
                return RedirectToAction("ReceptionistDashboard", "Dashboard");

            return RedirectToAction("Error", "Home", new { message = "دور المستخدم غير مدعوم." });
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminDashboard()
        {
            var model = new AdminDashboardViewModelModel
            {
                Stats = _dashboardHelper.GetDashboardStats(),
                ChartData = _dashboardHelper.GetChartData()
            };

            return View(model);
        }

        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> TraineeDashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // FIX #1: Check userId first before querying
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // FIX #2: Use async and check null before accessing .TraineeId
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null)
                return Unauthorized();

            var model = await _dashboardHelper.GetDashboardDataAsync(trainee.TraineeId, userId);
            model.TraineeId = trainee.TraineeId;
            return View(model);
        }

        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> TrainerDashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainer == null)
                return NotFound("Trainer profile not found.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return NotFound("User not found.");

            try
            {
                var viewModel = await _dashboardHelper.GetTrainerDashboardAsync(trainer.TrainerId, user.FullName);
                return View(viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TrainerDashboard Error] {ex.Message}");
                return StatusCode(500, "An error occurred while loading the dashboard.");
            }
        }

        [Authorize(Roles = "Receptionist")]
        public async Task<IActionResult> ReceptionistDashboard()
        {
            // FIX #3: Use injected _dashboardHelper instead of creating new instance manually
            ViewBag.ActiveStudents = _dashboardHelper.GetActiveStudents();
            ViewBag.MonthlyRevenue = _dashboardHelper.GetMonthlyRevenue();
            ViewBag.TotalCourses = _dashboardHelper.GetTotalCourses();

            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName");
            ViewData["TraineeId"] = new SelectList(
                _context.Trainees.Include(t => t.User), "TraineeId", "User.FullName");

            return View();
        }
    }
}
