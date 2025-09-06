using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Xml.Linq;
using TrainingCenterManagement_MVC.Data;
using Microsoft.EntityFrameworkCore;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class SitemapController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SitemapController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("sitemap.xml")]
        [Produces("application/xml")]
        public async Task<IActionResult> Index()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host.Value}";

            var urls = new List<(string loc, DateTime? lastmod)>();

            // Static important pages
            urls.Add((Url.Action("Index", "Home", null, Request.Scheme, Request.Host.Value), DateTime.UtcNow));
            urls.Add((Url.Action("Index", "Courses", null, Request.Scheme, Request.Host.Value), DateTime.UtcNow));

            // Add courses (details)
            var courses = await _context.Courses
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .ToListAsync();

            foreach (var c in courses)
            {
                var loc = Url.Action("Details", "Courses", new { id = c.CourseId }, Request.Scheme, Request.Host.Value);
                DateTime? lastmod = null;
                // handle when ReleaseDate might be default
                if (c.ReleaseDate != default(DateTime)) lastmod = c.ReleaseDate;
                else lastmod = c.CreatedDate;

                // ensure tuple types match
                urls.Add((loc, lastmod));
            }

            // Build XML
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var urlset = new XElement(ns + "urlset",
                from u in urls
                where !string.IsNullOrEmpty(u.loc)
                select new XElement(ns + "url",
                    new XElement(ns + "loc", u.loc),
                    u.lastmod != null ? new XElement(ns + "lastmod", u.lastmod.Value.ToString("yyyy-MM-dd")) : null,
                    new XElement(ns + "changefreq", "weekly"),
                    new XElement(ns + "priority", "0.6")
                )
            );

            var declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
            var xml = declaration + urlset.ToString();

            return Content(xml, "application/xml", Encoding.UTF8);
        }
    }
}
