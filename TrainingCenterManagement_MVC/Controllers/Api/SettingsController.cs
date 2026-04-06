using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Helpers;

namespace TrainingCenterManagement_MVC.Controllers.Api
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly ISettingsService _settingsService;

        public SettingsController(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var key = await _settingsService.GetAsync("JwtKey");
            
            return Ok(key);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string JwtKey)
        {
            if (string.IsNullOrWhiteSpace(JwtKey) || System.Text.Encoding.UTF8.GetByteCount(JwtKey) < 32)
            {
                
                
                return BadRequest("The JWT key must be at least 32 bytes long.");
            }

            await _settingsService.SetAsync("JwtKey", JwtKey);
            TempData["SuccessMessage"] = "JWT key updated successfully. Restart may be required for Bearer validation depending on configuration.";
            return Ok(new
            {
                message = "JWT key updated successfully. Restart may be required for Bearer validation depending on configuration."
                ,
                JwtKey = JwtKey
            });
        }
    }
}
