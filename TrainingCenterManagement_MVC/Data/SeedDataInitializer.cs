using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

public class SeedDataInitializer
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public SeedDataInitializer(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task SeedAllAsync()
    {
        await SeedCoursesAsync();
        await SeedLecturesAsync();
        await SeedAttendancesAsync();
        await SeedPaymentsAsync();
        await SeedExamsAndResultsAsync();
        await SeedCertificatesAsync();
      
    }

    private async Task SeedCoursesAsync()
    {
        if (_context.Courses.Any()) return;

        var admin = await _context.Admins.Include(a => a.User).FirstOrDefaultAsync();
        if (admin == null) return;

        _context.Courses.AddRange(new[]
        {
            new Course
            {
                CourseName = "Advanced C#",
                BatchNumber = 3,
                NumberOfLectures = 8,
                Price = 800,
                Description = "C# advanced concepts",
                VideoUrl = "https://example.com/advanced-csharp",
                ThumbnailUrl = "https://example.com/csharp-thumb",
                CreatedDate = DateTime.UtcNow,
                ReleaseDate = DateTime.UtcNow,
                AdminId = admin.AdminId
            }
        });

        await _context.SaveChangesAsync();
    }

    private async Task SeedLecturesAsync()
    {
        if (_context.Lectures.Any()) return;

        var course = await _context.Courses.FirstOrDefaultAsync();
        if (course == null) return;

        for (int i = 1; i <= course.NumberOfLectures; i++)
        {
            _context.Lectures.Add(new Lecture
            {
                CourseId = course.CourseId,
                Title = $"Lecture {i}",
                LectureDate = DateTime.UtcNow.AddDays(i),
                Description="This is Extented Courses",
                ThumbnailUrl="https://www.youtube.com",
                VideoUrl="https://www.youtube.com/1231"
                
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedAttendancesAsync()
    {
        if (_context.Presences.Any()) return;

        var trainee = await _context.Trainees.FirstOrDefaultAsync();
        var lectures = await _context.Lectures.ToListAsync();
        if (trainee == null || lectures.Count == 0) return;

        foreach (var lecture in lectures)
        {
            _context.Presences.Add(new Presence
            {
                LectureId = lecture.LectureId,
                TraineeId = trainee.TraineeId,
                IsPresent = true
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedPaymentsAsync()
    {
        if (_context.Payments.Any()) return;

        var trainee = await _context.Trainees.FirstOrDefaultAsync();
        var course = await _context.Courses.FirstOrDefaultAsync();
        if (trainee == null || course == null) return;

        _context.Payments.Add(new Payment
        {
            CourseId = course.CourseId,
            TraineeId = trainee.TraineeId,
            TotalAmount = (decimal) course.Price,
            CreatedDate = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    private async Task SeedExamsAndResultsAsync()
    {
        if (_context.Exams.Any()) return;

        var course = await _context.Courses.FirstOrDefaultAsync();
        var trainee = await _context.Trainees.FirstOrDefaultAsync();
        if (course == null || trainee == null) return;

        var exam = new Exam
        {
            ExamName = "Final Exam",
            CourseId = course.CourseId,
            ExamDate = DateTime.UtcNow
        };
        _context.Exams.Add(exam);
        await _context.SaveChangesAsync();

    }

    private async Task SeedCertificatesAsync()
    {
        if (_context.Certificates.Any()) return;

        var course = await _context.Courses.FirstOrDefaultAsync();
        var trainer = await _context.Trainers.FirstOrDefaultAsync();
        var trainee = await _context.Trainees.FirstOrDefaultAsync();
        var exam = await _context.Exams.FirstOrDefaultAsync();

        if (course == null || trainer == null || trainee == null || exam == null) return;

        _context.Certificates.Add(new Certificate
        {
            CourseId = course.CourseId,
            TrainerId = trainer.TrainerId,
            TraineeId = trainee.TraineeId,
            ExamId = exam.ExamId,
            Average = 85,
            Url = "https://example.com/cert.pdf"
        });

        await _context.SaveChangesAsync();
    }






    
}
