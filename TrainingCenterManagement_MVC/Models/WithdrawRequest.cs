using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainingCenterManagement_MVC.Models
{
    public enum WithdrawStatus { PendingReview = 0, PartiallyApproved = 1, FullyApproved = 2 }

    public class WithdrawRequest
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TraineeId { get; set; }
        public Trainee Trainee { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountUSD { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountSYP { get; set; }

        public string BarCodeImagePath { get; set; } = string.Empty;

        public WithdrawStatus Status { get; set; } = WithdrawStatus.PendingReview;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // How much has actually been sent back by admin via ShamCash
        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmountUSD { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmountSYP { get; set; }
    }
}
