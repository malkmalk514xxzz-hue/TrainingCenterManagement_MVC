using Microsoft.AspNetCore.Identity;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Helpers
{
    public class UserHelper : IUserHelper
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
       

        public UserHelper(
      UserManager<ApplicationUser> userManager,
      SignInManager<ApplicationUser> signInManager,
      RoleManager<IdentityRole> roleManager
      )
        {
            this._userManager = userManager;
            this._signInManager = signInManager;
            this._roleManager = roleManager;
           
        }

        public async Task<IdentityResult> AddUserAsync(ApplicationUser user, string password)
        {
            return await _userManager.CreateAsync(user, password);
        }

        public async Task AddUserToRoleAsync(ApplicationUser user, string roleName)
        {
            await _userManager.AddToRoleAsync(user, roleName);
        }

        public async Task<IdentityResult> ChangePasswordAsync(ApplicationUser user, string oldPassword, string newPassword)
        {
            return await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);
        }

        public async Task CheckRoleAsync(string roleName)
        {
            var roleExists = await _roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                await _roleManager.CreateAsync(new IdentityRole
                {
                    Name = roleName
                });
            }
        }

        public async Task<IdentityResult> ConfirmEmailAsync(ApplicationUser user, string token)
        {
            return await _userManager.ConfirmEmailAsync(user, token);
        }

        public async Task<string> GenerateEmailConfirmationTokenAsync(ApplicationUser user)
        {
            return await _userManager.GenerateEmailConfirmationTokenAsync(user);
        }

        public async Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user)
        {
            return await _userManager.GeneratePasswordResetTokenAsync(user);
        }

        public async Task<ApplicationUser> GetUserByEmailAsync(string email)
        {
            return await _userManager.FindByEmailAsync(email);
        }

        public async Task<ApplicationUser> GetUserByIdAsync(string userId)
        {
            return await _userManager.FindByIdAsync(userId);
        }

        public async Task<bool> IsUserInRoleAsync(ApplicationUser user, string roleName)
        {
            return await _userManager.IsInRoleAsync(user, roleName);
        }

        public async Task<SignInResult> LoginAsync(LoginViewModel model)
        {
            return await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                false);
        }

        public async Task LogoutAsync()
        {
            await _signInManager.SignOutAsync();
        }

        public async Task<IdentityResult> ResetPasswordAsync(ApplicationUser user, string token, string password)
        {
            return await _userManager.ResetPasswordAsync(user, token, password);
        }

        public async Task<IdentityResult> UpdateUserAsync(ApplicationUser user)
        {
            return await _userManager.UpdateAsync(user);
        }

        public async Task<SignInResult> ValidatePasswordAsync(ApplicationUser user, string password)
        {
            return await _signInManager.CheckPasswordSignInAsync(
                user,
                password,
                false);
        }

        public async Task RemoveUserFromRoleAsync(ApplicationUser user, string roleName)
        {
            await _userManager.RemoveFromRoleAsync(user, roleName);
        }

        public async Task<List<ApplicationUser>> GetAllUsersInRoleAsync(string roleName)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
            return usersInRole.ToList();
        }

      

        public async Task<IdentityResult> ResetPasswordWithoutTokenAsync(ApplicationUser user, string password)
        {
            // Update the password directly, creating a hash from the new password
            user.PasswordHash = _userManager.PasswordHasher.HashPassword(user, password);
            return await _userManager.UpdateAsync(user);
        }

        public async Task<string> GetRoleAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return roles.FirstOrDefault(); // Returns the first role associated with the user
        }

        //public async Task UpdateUserDataByRoleAsync(ApplicationUser user)
        //{
        //    var roles = await _userManager.GetRolesAsync(user);

            
        //     if (roles.Contains("Student"))
        //    {
        //        var student = await _studentRepository.GetStudentByUserIdAsync(user.Id);
        //        if (student != null)
        //        {
        //            student.FirstName = user.FirstName;
        //            student.LastName = user.LastName;
        //            await _studentRepository.UpdateAsync(student);
        //        }
        //    }
        //    else if (roles.Contains("Teacher"))
        //    {
        //        var teacher = await _teacherRepository.GetTeacherByUserIdAsync(user.Id);
        //        if (teacher != null)
        //        {
        //            teacher.FirstName = user.FirstName;
        //            teacher.LastName = user.LastName;
        //            await _teacherRepository.UpdateAsync(teacher);
        //        }
        //    }
        //}

       


    }
}
