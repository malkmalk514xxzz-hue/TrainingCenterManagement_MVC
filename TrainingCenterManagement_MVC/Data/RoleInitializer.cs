using Microsoft.AspNetCore.Identity;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Data
{
    public class RoleInitializer
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public RoleInitializer(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
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

            // ✅ إنشاء مستخدم Admin افتراضي
            await CreateUserIfNotExists("admin@site.com", "Admin123!", "System Admin", RoleType.Admin);

            // ✅ Trainer
            await CreateUserIfNotExists("trainer@site.com", "Trainer123!", "Trainer User", RoleType.Trainer);

            // ✅ Trainee
            await CreateUserIfNotExists("trainee@site.com", "Trainee123!", "Trainee User", RoleType.Trainee);

            // ✅ Receptionist
            await CreateUserIfNotExists("reception@site.com", "Reception123!", "Receptionist User", RoleType.Receptionist);
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
                Role = role
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role.ToString());
            }
        }
    }
}
