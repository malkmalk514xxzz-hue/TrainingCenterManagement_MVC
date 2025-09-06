using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Helpers
{
    public class SettingsService : ISettingsService
    {
        private readonly ApplicationDbContext _context;
        public SettingsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GetAsync(string key)
        {
            try
            {
                // If the AppSettings table does not exist yet (migrations not applied), catch and return null
                var s = await _context.AppSettings.FirstOrDefaultAsync(a => a.Key == key);
                return s?.Value;
            }
            catch (Exception)
            {
                // log optionally
                return null;
            }
        }

        public async Task SetAsync(string key, string value)
        {
            try
            {
                var s = await _context.AppSettings.FirstOrDefaultAsync(a => a.Key == key);
                if (s == null)
                {
                    s = new AppSetting { Key = key, Value = value };
                    _context.AppSettings.Add(s);
                }
                else
                {
                    s.Value = value;
                    _context.AppSettings.Update(s);
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception)
            {
                // If DB/table not ready, swallow to avoid crashing requests. Admin should run migrations to enable persistence.
            }
        }
    }
}
