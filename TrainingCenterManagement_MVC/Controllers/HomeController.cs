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

        public IActionResult Index()
        {
            // FIX: Filter soft-deleted courses so they don't appear on the landing page
            var featuredCourses = _context.Courses
                .Where(c => !c.IsDeleted)
                .ToList();

            var model = new HomeViewModel { Courses = featuredCourses };
            return View(model);
        }

        public IActionResult Error()
        {
            return View();
        }

        public IActionResult Error404()
        {
            return View();
        }
    }
}
