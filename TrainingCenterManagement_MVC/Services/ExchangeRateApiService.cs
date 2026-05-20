using System.Text.Json;

namespace TrainingCenterManagement_MVC.Services
{
    public class ExchangeRateApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        private static decimal _cachedRate = 130m;
        private static DateTime _lastFetch = DateTime.MinValue;
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public ExchangeRateApiService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<decimal> GetSypPerUsdAsync()
        {
            if (DateTime.UtcNow - _lastFetch < _cacheDuration)
                return _cachedRate;

            await _lock.WaitAsync();
            try
            {
                if (DateTime.UtcNow - _lastFetch < _cacheDuration)
                    return _cachedRate;

                var url = _configuration["ApiForTrans"] ?? "";
                if (string.IsNullOrEmpty(url)) return _cachedRate;

                var client = _httpClientFactory.CreateClient();
                var json = await client.GetStringAsync(url);
                var doc = JsonDocument.Parse(json);
                // {"usdsypd":{"value":13960,...}} → 13960 / 100 = 139.60 SYP per USD
                var value = doc.RootElement
                    .GetProperty("usdsypd")
                    .GetProperty("value")
                    .GetDecimal();
                _cachedRate = value / 100m;
                _lastFetch = DateTime.UtcNow;
            }
            catch { /* keep cached rate on any error */ }
            finally
            {
                _lock.Release();
            }

            return _cachedRate;
        }
    }
}
