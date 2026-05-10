using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Helpers
{
    /// <summary>Central place for all currency display and conversion logic.</summary>
    public static class CurrencyHelper
    {
        public static string GetLabel(PaymentCurrency c) => c switch
        {
            PaymentCurrency.USD => "دولار أمريكي (USD)",
            PaymentCurrency.EUR => "يورو (EUR)",
            _                   => "ليرة سورية (ل.س)"   // SYP
        };

        public static string GetSymbol(PaymentCurrency c) => c switch
        {
            PaymentCurrency.USD => "$",
            PaymentCurrency.EUR => "€",
            _                   => "ل.س"
        };

        public static string GetCode(PaymentCurrency c) => c switch
        {
            PaymentCurrency.USD => "USD",
            PaymentCurrency.EUR => "EUR",
            _                   => "SYP"
        };

        /// <summary>Parse default currency string from AppSettings → PaymentCurrency enum.</summary>
        public static PaymentCurrency ParseDefault(string? setting) => setting?.ToUpper() switch
        {
            "USD" => PaymentCurrency.USD,
            "EUR" => PaymentCurrency.EUR,
            _     => PaymentCurrency.SYP
        };

        /// <summary>Default exchange rates (SAR-free) relative to SYP as base.</summary>
        public static readonly Dictionary<PaymentCurrency, decimal> DefaultRates = new()
        {
            [PaymentCurrency.SYP] = 1m,
            [PaymentCurrency.USD] = 13000m,  // 1 USD = 13,000 ل.س
            [PaymentCurrency.EUR] = 14000m    // 1 EUR = 14,000 ل.س
        };

        /// <summary>Convert an amount in the given currency to SYP using provided rates.</summary>
        public static decimal ToSYP(decimal amount, PaymentCurrency currency,
            Dictionary<PaymentCurrency, decimal> rates)
        {
            if (currency == PaymentCurrency.SYP) return amount;
            return rates.TryGetValue(currency, out var rate) ? amount * rate : amount;
        }
    }
}
