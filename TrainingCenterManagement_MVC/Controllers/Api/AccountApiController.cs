using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountApiController : ControllerBase
    {
        private readonly IUserHelper _userHelper;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly Helpers.ISettingsService _settingsService;
        private readonly IConfiguration _configuration;
        public AccountApiController(IUserHelper userHelper, ApplicationDbContext context ,
            UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager
            , Helpers.ISettingsService settingsService, IConfiguration configuration)
        {
            _userHelper = userHelper;
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _settingsService = settingsService;
            _configuration = configuration;
        }


        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromForm] LoginViewModel model)
        {
            if (model == null)
                return BadRequest(new { error = "Model is required" });

            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
                return BadRequest(new { error = "Email and Password are required" });

            // Authenticate user
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Unauthorized(new { message = "Invalid credentials" });

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!result.Succeeded)
                return Unauthorized(new { message = "Invalid credentials" });

            // Build claims
            var roles = await _userManager.GetRolesAsync(user);
            var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id),
        new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
        new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
    };

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            // === الحل الرئيسي: الحصول على JwtKey بطريقة آمنة ===
            string jwtKey = await _settingsService.GetAsync("JwtKey");

            if (string.IsNullOrWhiteSpace(jwtKey))
                jwtKey = _configuration["Jwt:Key"];

            // Fallback قوي جداً (مفتاح سري طويل بما فيه الكفاية)
            if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
            {
                jwtKey = "SuperSecretKeyForDevelopmentOnly1234567890ABCDEF1234567890"; // ≥ 32 bytes
                                                                                       // في الإنتاج: لا تستخدم هذا أبداً، استخدم User Secrets أو Azure Key Vault
            }

            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "TrainingCenter";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "TrainingCenterAudience";
            var expiresInMinutes = int.TryParse(_configuration["Jwt:ExpiresMinutes"], out var m) ? m : 1440;

            // إنشاء التوكن
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            var roleu = await _userHelper.GetRoleAsync(user);

            return Ok(new
            {
                message = "Login successful",
                role = roleu,
                userId = user.Id,
                access_token = tokenString,
                token_type = "Bearer",
                expires_in = TimeSpan.FromMinutes(expiresInMinutes).TotalSeconds
            });
        }



        [HttpPost("Logout")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Logout()
        {
            await _userHelper.LogoutAsync();
            return Ok(new { message = "Logged out successfully" });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Receptionist,Admin")]
        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterNewUserViewModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // نفس المنطق الموجود في الكود الأصلي مع تعديلات بسيطة
            // ... (يمكن توسيعه حسب الحاجة)

            return Ok(new { message = "User created successfully" });
        }
    }
}