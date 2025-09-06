using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public HomeApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<HomeViewModel>> Index()
        {
            var featuredCourses = await _context.Courses.ToListAsync();
            var model = new HomeViewModel { Courses = featuredCourses };
            return Ok(model);
        }
    }
}
