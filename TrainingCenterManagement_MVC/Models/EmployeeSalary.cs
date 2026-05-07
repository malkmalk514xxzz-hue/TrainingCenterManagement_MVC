using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainingCenterManagement_MVC.Models
{
    public class EmployeeSalary
    {
        [Key]
        public Guid SalaryId { get; set; } = Guid.NewGuid();

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        /// <summary>"Trainer" or "Receptionist"</summary>
        [Required, MaxLength(30)]
        public string EmployeeRole { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlySalary { get; set; }

        public PaymentCurrency Currency { get; set; } = PaymentCurrency.SAR;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public ICollection<SalaryPayment> Payments { get; set; } = new List<SalaryPayment>();
    }
}
