using System.Threading.Tasks;

namespace TrainingCenterManagement_MVC.Helpers
{
    public interface ISettingsService
    {
        Task<string> GetAsync(string key);
        Task SetAsync(string key, string value);
    }
}
