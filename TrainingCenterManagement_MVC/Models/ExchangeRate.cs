using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainingCenterManagement_MVC.Models
{
    /// <summary>
    /// Stores how many SYP = 1 unit of the given currency.
    /// E.g. Currency=USD, RateToSYP=13000 means 1 USD = 13,000 ل.س.
    /// SYP itself is the base and has no entry here.
    /// </summary>
    public class ExchangeRate
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public PaymentCurrency Currency { get; set; }

        /// <summary>How many SYP equals 1 unit of this currency.</summary>
        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal RateToSYP { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string? UpdatedByUserId { get; set; }
        public ApplicationUser? UpdatedBy { get; set; }
    }
}
