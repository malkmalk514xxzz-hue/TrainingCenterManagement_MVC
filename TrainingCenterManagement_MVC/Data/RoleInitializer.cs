using Microsoft.AspNetCore.Identity;
using TrainingCenterManagement_MVC.Models;
using Microsoft.EntityFrameworkCore;

namespace TrainingCenterManagement_MVC.Data
{
    public class RoleInitializer
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public RoleInitializer(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _context = context;
        }

        public async Task SeedRolesAsync()
        {
            string[] roles = { "Admin", "Trainer", "Trainee", "Receptionist" };

            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // ✅ إنشاء مستخدمين افتراضيين
            await CreateUserIfNotExists("admin@site.com", "Admin123!", "System Admin", RoleType.Admin);
            await CreateUserIfNotExists("trainer@site.com", "Trainer123!", "Trainer User", RoleType.Trainer);
            await CreateUserIfNotExists("trainee@site.com", "Trainee123!", "Trainee User", RoleType.Trainee);
            await CreateUserIfNotExists("reception@site.com", "Reception123!", "Receptionist User", RoleType.Receptionist);


            // ✅ ربط المستخدمين بالكورسات
            var trainee = await _context.Trainees
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.User.Email == "trainee@site.com");
            var trainer = await _context.Trainers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.User.Email == "trainer@site.com");

            if (trainee != null && trainer != null && _context.Courses.Any())
            {
                var courses = await _context.Courses.ToListAsync();

                // التحقق من وجود CourseTrainees قبل الإدراج
                if (!_context.CourseTrainees.Any(ct => ct.TraineeId == trainee.TraineeId))
                {
                    var courseTraineeEntries = courses.Select(c => new CourseTrainee
                    {
                        CourseId = c.CourseId,
                        TraineeId = trainee.TraineeId
                    }).ToList();
                    _context.CourseTrainees.AddRange(courseTraineeEntries);
                }

                // التحقق من وجود CourseTrainers قبل الإدراج
                if (!_context.CourseTrainers.Any(ct => ct.TrainerId == trainer.TrainerId))
                {
                    var courseTrainerEntries = courses.Select(c => new CourseTrainer
                    {
                        CourseId = c.CourseId,
                        TrainerId = trainer.TrainerId
                    }).ToList();
                    _context.CourseTrainers.AddRange(courseTrainerEntries);
                }

                await _context.SaveChangesAsync();

            // Seed default JwtKey setting if not exists
            var jwtSetting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == "JwtKey");
            if (jwtSetting == null)
            {
                _context.AppSettings.Add(new Models.AppSetting
                {
                    Key = "JwtKey",
                    Value = "VerySecretKey12345VerySecretKey12345" // default 32-char key for development
                });
                await _context.SaveChangesAsync();
            }
            }
        }

        private async Task CreateUserIfNotExists(string email, string password, string fullName, RoleType role)
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
                return;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                Role = role,
                BirthDate = new DateTime(1997,6,10),
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role.ToString());

                // ربط المستخدم بالدور المناسب
                switch (role)
                {
                    case RoleType.Admin:
                        _context.Admins.Add(new Admin { UserId = user.Id, User = user });
                        break;
                    case RoleType.Trainer:
                        _context.Trainers.Add(new Trainer
                        {
                            UserId = user.Id,
                            User = user,
                            Specialty = "Programming",
                            YearsOfExperience = 5,
                            BusinessLink = "https://example.com"
                        });
                        break;
                    case RoleType.Trainee:
                        _context.Trainees.Add(new Trainee
                        {
                            UserId = user.Id,
                            User = user,
                           // BirthDate = DateTime.Now.AddYears(-20)
                        });
                        break;
                    case RoleType.Receptionist:
                        _context.Receptionists.Add(new Receptionist { UserId = user.Id, User = user });
                        break;
                }
                await _context.SaveChangesAsync();
            }
        }
    }
}