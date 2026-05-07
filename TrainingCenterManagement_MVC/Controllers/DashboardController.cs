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
        private readonly TraineeDashboardHelper _traineeHelper;
        private readonly TrainerDashboardHelper _trainerHelper;
        private readonly AdminDashboardHelper _adminHelper;
        private readonly ReceptionistDashboardHelper _receptionistHelper;

        public DashboardController(
            ApplicationDbContext context,
            DashboardHelper dashboardHelper,
            TraineeDashboardHelper traineeHelper,
            TrainerDashboardHelper trainerHelper,
            AdminDashboardHelper adminHelper,
            ReceptionistDashboardHelper receptionistHelper)
        {
            _context = context;
            _dashboardHelper = dashboardHelper;
            _traineeHelper = traineeHelper;
            _trainerHelper = trainerHelper;
            _adminHelper = adminHelper;
            _receptionistHelper = receptionistHelper;
        }

        public async Task<IActionResult> Index()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Login", "Account");

            if (User.IsInRole("Trainer"))      return RedirectToAction("TrainerDashboard");
            if (User.IsInRole("Trainee"))      return RedirectToAction("TraineeDashboard");
            if (User.IsInRole("Admin"))        return RedirectToAction("AdminDashboard");
            if (User.IsInRole("Receptionist")) return RedirectToAction("ReceptionistDashboard");

            return RedirectToAction("Error", "Home", new { message = "دور المستخدم غير مدعوم." });
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminDashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var model = await _adminHelper.GetAdminDashboardAsync(userId);
                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdminDashboard Error] {ex.Message}");
                return StatusCode(500, "حدث خطأ أثناء تحميل لوحة التحكم.");
            }
        }

        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> TraineeDashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return Unauthorized();

            var model = await _traineeHelper.GetDashboardDataAsync(trainee.TraineeId, userId);
            model.TraineeId = trainee.TraineeId;
            return View(model);
        }

        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> TrainerDashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainer == null) return NotFound("Trainer profile not found.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound("User not found.");

            try
            {
                var viewModel = await _trainerHelper.GetTrainerDashboardAsync(trainer.TrainerId, user.FullName);
                viewModel.TrainerId = trainer.TrainerId;
                viewModel.Email = user.Email ?? string.Empty;
                viewModel.YearsOfExperience = trainer.YearsOfExperience;
                viewModel.BusinessLink = trainer.BusinessLink;
                viewModel.ProfilePictureUrl = !string.IsNullOrEmpty(user.ProfilePictureUrl)
                    ? user.ProfilePictureUrl
                    : "/images/default-profile.png";
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                var model = await _receptionistHelper.GetDashboardAsync(userId);
                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReceptionistDashboard Error] {ex.Message}");
                return StatusCode(500, "حدث خطأ أثناء تحميل لوحة التحكم.");
            }
        }
    }
}
