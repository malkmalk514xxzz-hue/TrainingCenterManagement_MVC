using Microsoft.AspNetCore.Mvc;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class HomeApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public HomeApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Home
        [HttpGet]
        public IActionResult Index()
        {
            // return featured courses as JSON for API clients
            var featuredCourses = _context.Courses.ToList();
            var model = new HomeViewModel { Courses = featuredCourses };
            return Ok(model);
        }

        // GET: api/Home/error
        [HttpGet("error")]
        public IActionResult Error()
        {
            return Problem(detail: "An error occurred.");
        }
    }
}
