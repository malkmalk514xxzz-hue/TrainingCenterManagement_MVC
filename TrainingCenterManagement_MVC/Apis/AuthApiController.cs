using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.ViewModels;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Apis
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthApiController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;
        private readonly IUserHelper _userHelper;

        public AuthApiController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, IConfiguration config, ApplicationDbContext context, IUserHelper userHelper)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _config = config;
            _context = context;
            _userHelper = userHelper;
        }

        public class LoginRequest
        {
            public string UserName { get; set; }
            public string Password { get; set; }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest model)
        {
            if (model == null) return BadRequest();
            var user = await _userManager.FindByNameAsync(model.UserName);
            if (user == null) return Unauthorized(new { message = "Invalid credentials" });

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!result.Succeeded) return Unauthorized(new { message = "Invalid credentials" });

            var token = GenerateJwtToken(user);
            return Ok(new { token, userId = user.Id, role = user.Role.ToString(), fullName = user.FullName });
        }

        private string GenerateJwtToken(ApplicationUser user)
        {
            var jwtKey = _config["Jwt:Key"] ?? "VerySecretKey12345";
            var jwtIssuer = _config["Jwt:Issuer"] ?? "TrainingCenter";
            var jwtAudience = _config["Jwt:Audience"] ?? "TrainingCenterAudience";

            // Ensure signing key meets minimum size requirement (>= 256 bits / 32 bytes).
            var keyBytes = Encoding.UTF8.GetBytes(jwtKey ?? string.Empty);
            if (keyBytes.Length < 32)
            {
                // Derive a 32-byte key deterministically from provided secret using SHA-256
                using var sha = System.Security.Cryptography.SHA256.Create();
                keyBytes = sha.ComputeHash(keyBytes);
            }

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };
            var key = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public class QrScanRequest
        {
            public string Token { get; set; }
            public string UserId { get; set; }
        }

        [HttpPost("qr-scan")]
        [AllowAnonymous]
        public async Task<IActionResult> QrScan([FromBody] QrScanRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.Token) || string.IsNullOrEmpty(req.UserId)) return BadRequest();
            if (!Guid.TryParse(req.Token, out var tokenGuid)) return BadRequest(new { message = "Invalid token format" });
            var qr = await _context.QrLoginTokens.FindAsync(tokenGuid);
            if (qr == null || qr.Used || qr.ExpiresAt < DateTime.UtcNow) return BadRequest(new { message = "Invalid or expired token" });

            // mark used
            qr.Used = true;
            await _context.SaveChangesAsync();

            // return success - the Android client can then call /api/auth/login with credentials or request a token
            return Ok(new { message = "QR accepted", courseId = qr.CourseId });
        }

        // Register via API - reuse existing registration logic
        [HttpPost("register")]
        [Authorize(Roles = "Receptionist,Admin")]
        public async Task<IActionResult> Register([FromBody] RegisterNewUserViewModel model)
        {
            if (model == null) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existing = await _userHelper.GetUserByEmailAsync(model.Username);
            if (existing != null) return Conflict(new { message = "This email is already registered." });

            var user = new ApplicationUser
            {
                FullName = model.FullName,
                Email = model.Username,
                UserName = model.Username,
                BirthDate = model.BirthDate,
                PhoneNumber = model.PhoneNumber,
            };

            var result = await _userHelper.AddUserAsync(user, model.Password);
            if (result != IdentityResult.Success)
            {
                return StatusCode(500, new { message = "The user could not be created." });
            }

            string role = model.Role == RoleType.Trainee ? "Trainee" : "Trainer";
            await _userHelper.AddUserToRoleAsync(user, role);

            if (model.Role == RoleType.Trainee)
            {
                await _context.Trainees.AddAsync(new Trainee { User = user, UserId = user.Id });
            }
            else if (model.Role == RoleType.Trainer)
            {
                await _context.Trainers.AddAsync(new Trainer { User = user, UserId = user.Id });
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "User created successfully." });
        }
    }
}
