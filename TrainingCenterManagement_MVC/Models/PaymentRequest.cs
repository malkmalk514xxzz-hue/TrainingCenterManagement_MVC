using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public enum OnlinePaymentMethod
    {
        ShamCash = 1,
        Binance  = 2
    }

    public enum PaymentRequestStatus
    {
        Pending  = 1,
        Approved = 2,
        Rejected = 3
    }

    public class PaymentRequest
    {
        [Key]
        public Guid RequestId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid TraineeId { get; set; }
        public Trainee Trainee { get; set; } = null!;

        [Required]
        public Guid CourseId { get; set; }
        public Course Course { get; set; } = null!;

        [Required]
        public decimal Amount { get; set; }

        public PaymentCurrency Currency { get; set; } = PaymentCurrency.SYP;

        public OnlinePaymentMethod Method { get; set; }

        // Path to uploaded receipt file (PDF or image), relative to wwwroot
        [MaxLength(500)]
        public string? ReceiptFilePath { get; set; }

        // Optional: transaction ID / hash entered by student (mainly for Binance)
        [MaxLength(200)]
        public string? TransactionReference { get; set; }

        [MaxLength(500)]
        public string? StudentNotes { get; set; }

        public PaymentRequestStatus Status { get; set; } = PaymentRequestStatus.Pending;

        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        [MaxLength(500)]
        public string? AdminNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }

        [MaxLength(450)]
        public string? ProcessedByAdminId { get; set; }

        // Auto-extracted fields from the uploaded receipt PDF
        [MaxLength(200)]
        public string? RcptSenderName { get; set; }

        [MaxLength(200)]
        public string? RcptRecipientName { get; set; }

        [MaxLength(200)]
        public string? RcptRecipientAccount { get; set; }

        [MaxLength(100)]
        public string? RcptAmount { get; set; }

        [MaxLength(50)]
        public string? RcptPaymentDate { get; set; }

        [MaxLength(100)]
        public string? RcptOperationNumber { get; set; }
    }
}
