using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Xml.Linq;
using TrainingCenterManagement_MVC.Data;

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
            var scheme = Request.Scheme;
            var host   = Request.Host.Value;
            var now    = DateTime.UtcNow;

            var entries = new List<SitemapEntry>();

            // ── الصفحات الثابتة بأولوياتها ───────────────────────────
            entries.Add(new(Url.Action("Index",  "Home",    null, scheme, host), now,        "daily",   "1.0"));
            entries.Add(new(Url.Action("Index",  "Courses", null, scheme, host), now,        "daily",   "0.9"));
            entries.Add(new(Url.Action("Verify", "Certificates", null, scheme, host), now,  "monthly", "0.7"));
            entries.Add(new(Url.Action("Search", "Certificates", null, scheme, host), now,  "monthly", "0.6"));
            entries.Add(new(Url.Action("Index",  "Trainers", null, scheme, host), now,      "monthly", "0.6"));

            // ── تفاصيل الكورسات ──────────────────────────────────────
            var courses = await _context.Courses
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .Select(c => new { c.CourseId, c.ReleaseDate, c.CreatedDate })
                .ToListAsync();

            foreach (var c in courses)
            {
                var loc     = Url.Action("Details", "Courses", new { id = c.CourseId }, scheme, host);
                var lastmod = c.ReleaseDate != default ? c.ReleaseDate : c.CreatedDate;
                entries.Add(new(loc, lastmod, "weekly", "0.8"));
            }

            // ── الامتحانات المنشورة ───────────────────────────────────
            var exams = await _context.Exams
                .AsNoTracking()
                .Where(e => !e.IsDeleted && e.IsPublished)
                .Select(e => new { e.ExamId, e.UpdatedAt, e.CreatedAt })
                .ToListAsync();

            foreach (var e in exams)
            {
                var loc     = Url.Action("Details", "Exams", new { id = e.ExamId }, scheme, host);
                var lastmod = e.UpdatedAt != default ? e.UpdatedAt : e.CreatedAt;
                entries.Add(new(loc, lastmod, "weekly", "0.6"));
            }

            // ── تفاصيل المدربين ───────────────────────────────────────
            var trainers = await _context.Trainers
                .AsNoTracking()
                .Select(t => new { t.TrainerId })
                .ToListAsync();

            foreach (var t in trainers)
            {
                var loc = Url.Action("Details", "Trainers", new { id = t.TrainerId }, scheme, host);
                entries.Add(new(loc, null, "monthly", "0.5"));
            }

            // ── بناء XML ──────────────────────────────────────────────
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var urlset = new XElement(ns + "urlset",
                entries
                    .Where(u => !string.IsNullOrEmpty(u.Loc))
                    .Select(u => new XElement(ns + "url",
                        new XElement(ns + "loc",        u.Loc),
                        u.LastMod.HasValue
                            ? new XElement(ns + "lastmod", u.LastMod.Value.ToString("yyyy-MM-dd"))
                            : null,
                        new XElement(ns + "changefreq", u.ChangeFreq),
                        new XElement(ns + "priority",   u.Priority)
                    ))
            );

            var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + urlset;
            return Content(xml, "application/xml", Encoding.UTF8);
        }

        private record SitemapEntry(string? Loc, DateTime? LastMod, string ChangeFreq, string Priority);
    }
}
