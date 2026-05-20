using System;
using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models // استبدل YourProjectName باسم مشروعك الحقيقي
{
    // 1. تحديد نوع العملية (دفع أو قبض)
    public enum TransactionType
    {
        Deposit = 1,  // قبض (+)
        Withdraw = 2  // دفع (-)
    }

    // 2. تحديد نوع العملة
    public enum CurrencyType
    {
        SYP = 1,
        USD = 2,
        Unknown = 0
    }

    public class shamCashTranslation
    {
        [Key]
        public int id { get; set; }
        public string userName { get; set; }
        public string transactionId { get; set; }
        public string registrationDate { get; set; }
        public decimal amountValue { get; set; }
        public TransactionType transactionType { get; set; } // enum
        public CurrencyType currencyType { get; set; }       // enum

        public string notes { get; set; }
    }

}