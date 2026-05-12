using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var allCourses = await _context.Courses
                .Where(c => !c.IsDeleted)
                .Include(c => c.CourseTrainees)
                .Include(c => c.Ratings)
                .ToListAsync();

            var featuredTrainers = await _context.Trainers
                .Include(t => t.User)
                .Include(t => t.CourseTrainers)
                .Take(6)
                .ToListAsync();

            var model = new HomeViewModel
            {
                Courses = allCourses,
                FeaturedCourses = allCourses.Where(c => c.IsFeatured).Take(6).ToList(),
                FeaturedTrainers = featuredTrainers,
                TotalCourses = allCourses.Count,
                TotalTrainees = await _context.Trainees.CountAsync(),
                TotalTrainers = await _context.Trainers.CountAsync(),
                TotalLectures = await _context.Lectures.CountAsync(),
                TotalCertificates = await _context.Certificates.CountAsync(c => !c.IsDeleted),
            };

            if (!model.FeaturedCourses.Any())
                model.FeaturedCourses = allCourses.Take(6).ToList();

            return View(model);
        }

        public IActionResult Error() => View();
        public IActionResult Error404() => View();
    }
}
