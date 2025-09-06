using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly Helpers.ISettingsService _settingsService;

        public AuthApiController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IConfiguration configuration, Helpers.ISettingsService settingsService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _settingsService = settingsService;
        }

        [HttpPost("token")]
        [Consumes("application/json")]
        public async Task<IActionResult> GetToken([FromBody] System.Text.Json.JsonElement body)
        {
            // Support both direct LoginViewModel JSON and wrapped { "model": { ... } } which some clients/Swagger may send.
            System.Text.Json.JsonElement modelElement = body;
            if (body.ValueKind == System.Text.Json.JsonValueKind.Object && body.TryGetProperty("model", out var wrapped))
            {
                modelElement = wrapped;
            }

            LoginViewModel? model = null;
            try
            {
                model = System.Text.Json.JsonSerializer.Deserialize<LoginViewModel>(modelElement.GetRawText(), new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (System.Text.Json.JsonException)
            {
                return BadRequest(new { error = "Invalid JSON payload" });
            }

            if (model == null)
            {
                return BadRequest(new { error = "Model is required" });
            }

            // Manual validation for required properties
            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
            {
                return BadRequest(new { error = "Email and Password are required" });
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return Unauthorized(new { message = "Invalid credentials" });

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!result.Succeeded) return Unauthorized(new { message = "Invalid credentials" });

            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Read JWT settings - prefer value stored in DB (editable by admin)
            var jwtKey = await _settingsService.GetAsync("JwtKey");
            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                jwtKey = _configuration["Jwt:Key"];
            }
            if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
            {
                // Fallback development key (replace in production via configuration or admin settings)
                jwtKey = "VerySecretKey12345VerySecretKey12345"; // 32 chars
            }
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "TrainingCenter";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "TrainingCenterAudience";
            var expiresInMinutes = int.TryParse(_configuration["Jwt:ExpiresMinutes"], out var m) ? m : 1440; // default 24h

            // create token
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                access_token = tokenString,
                token_type = "Bearer",
                expires_in = TimeSpan.FromMinutes(expiresInMinutes).TotalSeconds
            });
        }

        // Alternative endpoint for Swagger/form testing: accepts form-data or x-www-form-urlencoded
        [HttpPost("token-form")]
        public async Task<IActionResult> GetTokenFromForm([FromForm] LoginViewModel model)
        {
            if (model == null) return BadRequest(new { error = "Model is required" });
            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
                return BadRequest(new { error = "Email and Password are required" });

            // authenticate
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return Unauthorized(new { message = "Invalid credentials" });
            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!result.Succeeded) return Unauthorized(new { message = "Invalid credentials" });

            // Build claims
            var roles = await _userManager.GetRolesAsync(user);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
            };
            foreach (var role in roles) claims.Add(new Claim(ClaimTypes.Role, role));

            // Read JWT settings - prefer value stored in DB (editable by admin)
            var jwtKey = await _settingsService.GetAsync("JwtKey");
            if (string.IsNullOrWhiteSpace(jwtKey)) jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
                jwtKey = "VerySecretKey12345VerySecretKey12345";
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "TrainingCenter";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "TrainingCenterAudience";
            var expiresInMinutes = int.TryParse(_configuration["Jwt:ExpiresMinutes"], out var m) ? m : 1440;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(issuer: jwtIssuer, audience: jwtAudience, claims: claims, expires: DateTime.UtcNow.AddMinutes(expiresInMinutes), signingCredentials: creds);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return Ok(new { access_token = tokenString, token_type = "Bearer", expires_in = TimeSpan.FromMinutes(expiresInMinutes).TotalSeconds });
        }
    }
}
