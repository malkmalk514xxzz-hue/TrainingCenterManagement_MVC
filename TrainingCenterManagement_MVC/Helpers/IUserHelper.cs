using Microsoft.AspNetCore.Identity;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Helpers
{
    public interface IUserHelper
    {
        // Gets a user by email.
        Task<ApplicationUser> GetUserByEmailAsync(string email);

        // Adds a new user with a password.
        Task<IdentityResult> AddUserAsync(ApplicationUser user, string password);

        // Logs in a user with LoginViewModel data.
        Task<SignInResult> LoginAsync(LoginViewModel model);

        // Logs out the current user.
        Task LogoutAsync();

        // Updates an existing user.
        Task<IdentityResult> UpdateUserAsync(ApplicationUser user);

        // Changes a user's password.
        Task<IdentityResult> ChangePasswordAsync(ApplicationUser user, string oldPassword, string newPassword);

        // Checks if a role exists, creates it if not.
        Task CheckRoleAsync(string roleName);

        // Adds a user to a specific role.
        Task AddUserToRoleAsync(ApplicationUser user, string roleName);

        // Checks if a user is in a specific role.
        Task<bool> IsUserInRoleAsync(ApplicationUser user, string roleName);

        // Validates a user's password.
        Task<SignInResult> ValidatePasswordAsync(ApplicationUser user, string password);

        // Generates an email confirmation token.
        Task<string> GenerateEmailConfirmationTokenAsync(ApplicationUser user);

        // Confirms a user's email with a token.
        Task<IdentityResult> ConfirmEmailAsync(ApplicationUser user, string token);

        // Gets a user by their ID.
        Task<ApplicationUser> GetUserByIdAsync(string userId);

        // Generates a password reset token.
        Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user);

        // Resets a user's password with a token.
        Task<IdentityResult> ResetPasswordAsync(ApplicationUser user, string token, string password);

        // Removes a user from a role.
        Task RemoveUserFromRoleAsync(ApplicationUser user, string roleName);

        // Gets all users in a specific role.
        Task<List<ApplicationUser>> GetAllUsersInRoleAsync(string roleName);

        

        Task<IdentityResult> ResetPasswordWithoutTokenAsync(ApplicationUser user, string password);

        Task<string> GetRoleAsync(ApplicationUser user);

        //Task UpdateUserDataByRoleAsync(ApplicationUser user);

       

    }
}
