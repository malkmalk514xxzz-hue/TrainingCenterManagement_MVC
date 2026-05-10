using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.ViewModels
{
    // ─── Exchange Rates ───────────────────────────────────────────────────────

    public class ExchangeRateIndexViewModel
    {
        public List<ExchangeRateRow> Rates { get; set; } = new();
        public string DefaultCurrency { get; set; } = "SYP";
    }

    public class ExchangeRateRow
    {
        public Guid Id { get; set; }
        public PaymentCurrency Currency { get; set; }
        public string CurrencyLabel { get; set; } = string.Empty;
        public string CurrencySymbol { get; set; } = string.Empty;
        public decimal RateToSYP { get; set; }       // 1 unit = X ل.س (SYP)
        public decimal RateFromSYP { get; set; }     // 1 ل.س = X units
        public DateTime UpdatedAt { get; set; }
        public string UpdatedByName { get; set; } = string.Empty;
    }

    // ─── Student Financial Accounts ──────────────────────────────────────────

    public class StudentAccountsIndexViewModel
    {
        public List<StudentAccountSummary> Students { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public decimal TotalPending { get; set; }
        public int StudentsWithDebt { get; set; }
    }

    public class StudentAccountSummary
    {
        public Guid TraineeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfilePic { get; set; }
        public int EnrolledCourses { get; set; }
        public decimal TotalOwed { get; set; }        // SAR
        public decimal TotalPaid { get; set; }        // SAR
        public decimal RemainingBalance { get; set; } // SAR (positive = owes money)
    }

    public class StudentAccountDetailsViewModel
    {
        public Guid TraineeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfilePic { get; set; }
        public List<CourseDebtEntry> Courses { get; set; } = new();
        public List<PaymentHistoryEntry> PaymentHistory { get; set; } = new();
        public decimal TotalOwed { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal RemainingBalance { get; set; }
    }

    public class CourseDebtEntry
    {
        public Guid CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public decimal CoursePrice { get; set; }      // SAR
        public decimal TotalPaidSAR { get; set; }
        public decimal RemainingDebt { get; set; }
        public bool IsFullyPaid => RemainingDebt <= 0;
    }

    public class PaymentHistoryEntry
    {
        public Guid PaymentId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public decimal OriginalAmount { get; set; }
        public string OriginalCurrency { get; set; } = "ل.س";
        public decimal AmountInSAR { get; set; }
        public string? Notes { get; set; }
        public DateTime Date { get; set; }
    }

    // ─── Salary Management ────────────────────────────────────────────────────

    public class SalaryIndexViewModel
    {
        public List<EmployeeSalaryRow> Employees { get; set; } = new();
        public decimal TotalMonthlyPayroll { get; set; }
        public decimal PaidThisMonth { get; set; }
        public decimal PendingThisMonth { get; set; }
        public int CurrentMonth { get; set; }
        public int CurrentYear { get; set; }
    }

    public class EmployeeSalaryRow
    {
        public Guid SalaryId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfilePic { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public decimal MonthlySalary { get; set; }
        public string CurrencyLabel { get; set; } = "ل.س";
        public PaymentCurrency Currency { get; set; }
        public bool PaidThisMonth { get; set; }
        public DateTime? LastPaidAt { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
    }

    public class SalaryDetailsViewModel
    {
        public Guid SalaryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfilePic { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public decimal MonthlySalary { get; set; }
        public PaymentCurrency Currency { get; set; }
        public string CurrencyLabel { get; set; } = "ل.س";
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public List<SalaryPaymentRow> PaymentHistory { get; set; } = new();
        public decimal TotalPaid { get; set; }
    }

    public class SalaryPaymentRow
    {
        public Guid Id { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal Amount { get; set; }
        public string CurrencyLabel { get; set; } = "ل.س";
        public DateTime PaidAt { get; set; }
        public string PaidByName { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    // ─── Create/Edit forms ────────────────────────────────────────────────────

    public class CreateSalaryViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public decimal MonthlySalary { get; set; }
        public PaymentCurrency Currency { get; set; } = PaymentCurrency.SYP;
        public string? Notes { get; set; }
    }

    public class PaySalaryViewModel
    {
        public Guid SalaryId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal Amount { get; set; }
        public PaymentCurrency Currency { get; set; } = PaymentCurrency.SYP;
        public string? Notes { get; set; }
    }
}
