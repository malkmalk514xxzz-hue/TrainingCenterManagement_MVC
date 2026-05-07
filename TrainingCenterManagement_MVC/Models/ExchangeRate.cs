using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainingCenterManagement_MVC.Models
{
    /// <summary>
    /// Stores how many SAR = 1 unit of the given currency.
    /// E.g. Currency=USD, RateToSAR=3.75 means 1 USD = 3.75 SAR.
    /// SAR itself is the base and has no entry here.
    /// </summary>
    public class ExchangeRate
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public PaymentCurrency Currency { get; set; }

        /// <summary>How many SAR equals 1 unit of this currency.</summary>
        [Required]
        [Column(TypeName = "decimal(18,6)")]
        public decimal RateToSAR { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string? UpdatedByUserId { get; set; }
        public ApplicationUser? UpdatedBy { get; set; }
    }
}
