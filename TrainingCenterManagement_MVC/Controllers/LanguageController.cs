using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class LanguageController : Controller
    {
        public IActionResult ChangeLanguage(string lang)
        {
            if (string.IsNullOrEmpty(lang))
            {
                lang = "en";
            }

            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture(lang);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(lang);

            Response.Cookies.Append("Language", lang, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });

            bool isRtl = lang == "ar";
            Response.Cookies.Append("IsRTL", isRtl.ToString(), new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });

            return Redirect(Request.GetTypedHeaders().Referer?.ToString() ?? "/");
        }
    }
}
