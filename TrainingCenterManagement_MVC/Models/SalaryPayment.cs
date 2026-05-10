using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainingCenterManagement_MVC.Models
{
    public class SalaryPayment
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid SalaryId { get; set; }
        public EmployeeSalary Salary { get; set; } = null!;

        [Required]
        [Range(1, 12)]
        public int Month { get; set; }

        [Required]
        public int Year { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public PaymentCurrency Currency { get; set; } = PaymentCurrency.SYP;

        public DateTime PaidAt { get; set; } = DateTime.UtcNow;

        public string? PaidByAdminId { get; set; }
        public ApplicationUser? PaidByAdmin { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
