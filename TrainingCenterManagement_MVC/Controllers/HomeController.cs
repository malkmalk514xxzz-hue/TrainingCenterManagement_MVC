using Microsoft.AspNetCore.Mvc;
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
        public IActionResult Index()
        {
            // جلب الكورسات المميزة فقط
            var featuredCourses = _context.Courses.Where(c => c.IsFeatured).ToList(); // أو من قاعدة البيانات: _db.Courses.Where(c => c.IsFeatured).ToList();
            var model = new HomeViewModel { Courses = featuredCourses }; // افترض نموذج HomeViewModel يحتوي على List<Course> Courses
            return View(model);
        }
        public IActionResult Error()
        {
            return View();
        }
    }
}
