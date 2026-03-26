using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly DashboardHelper _dashboardHelper;

        public DashboardApiController(ApplicationDbContext context, DashboardHelper dashboardHelper)
        {
            _context = context;
            _dashboardHelper = dashboardHelper;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (User.IsInRole("Trainer")) return Ok(new { redirectTo = "/api/Dashboard/TrainerDashboard" });
            if (User.IsInRole("Trainee")) return Ok(new { redirectTo = "/api/Dashboard/TraineeDashboard" });
            if (User.IsInRole("Admin")) return Ok(new { redirectTo = "/api/Dashboard/AdminDashboard" });
            if (User.IsInRole("Receptionist")) return Ok(new { redirectTo = "/api/Dashboard/ReceptionistDashboard" });

            return BadRequest(new { message = "دور المستخدم غير مدعوم." });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("AdminDashboard")]
        public IActionResult GetAdminDashboard()
        {
            var model = new AdminDashboardViewModelModel
            {
                Stats = _dashboardHelper.GetDashboardStats(),
                ChartData = _dashboardHelper.GetChartData()
            };
            return Ok(model);
        }

        [Authorize(Roles = "Trainee")]
        [HttpGet("TraineeDashboard")]
        public async Task<IActionResult> GetTraineeDashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return Unauthorized();

            var model = await _dashboardHelper.GetDashboardDataAsync(trainee.TraineeId, userId);
            return Ok(model);
        }

        [Authorize(Roles = "Trainer")]
        [HttpGet("TrainerDashboard")]
        public async Task<IActionResult> GetTrainerDashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainer == null) return NotFound(new { message = "Trainer not found" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound(new { message = "User not found" });

            var model = await _dashboardHelper.GetTrainerDashboardAsync(trainer.TrainerId, user.FullName);
            return Ok(model);
        }

        [Authorize(Roles = "Receptionist")]
        [HttpGet("ReceptionistDashboard")]
        public IActionResult GetReceptionistDashboard()
        {
            var helper = new DashboardHelper(_context);
            return Ok(new
            {
                ActiveStudents = helper.GetActiveStudents(),
                MonthlyRevenue = helper.GetMonthlyRevenue(),
                TotalCourses = helper.GetTotalCourses()
            });
        }
    }
}