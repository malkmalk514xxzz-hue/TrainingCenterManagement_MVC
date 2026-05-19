using System.Text;
using UglyToad.PdfPig;

namespace TrainingCenterManagement_MVC.Helpers
{
    public class ExtractedReceiptData
    {
        public string? SenderName        { get; set; }
        public string? RecipientName     { get; set; }
        public string? RecipientAccount  { get; set; }
        public string? Amount            { get; set; }
        public string? PaymentDate       { get; set; }
        public string? OperationNumber   { get; set; }
        public bool    IsEmpty =>
            string.IsNullOrWhiteSpace(SenderName) &&
            string.IsNullOrWhiteSpace(RecipientName) &&
            string.IsNullOrWhiteSpace(Amount);
    }

    public static class ReceiptExtractor
    {
        public static ExtractedReceiptData ExtractFromPdf(string filePath)
        {
            var result = new ExtractedReceiptData();
            try
            {
                using var doc = PdfDocument.Open(filePath);
                var sb = new StringBuilder();
                foreach (var page in doc.GetPages())
                    sb.AppendLine(page.Text);

                var text = sb.ToString();
                result = ParseShamCashText(text);
            }
            catch { }
            return result;
        }

        private static ExtractedReceiptData ParseShamCashText(string text)
        {
            var data = new ExtractedReceiptData();
            if (string.IsNullOrWhiteSpace(text)) return data;

            // Normalize: collapse multiple spaces / newlines
            var lines = text
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            // Strategy: look for label lines, value is on the SAME or NEXT line
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var next = i + 1 < lines.Count ? lines[i + 1] : "";

                // اسم المرسل
                if (ContainsAny(line, "اسم المرسل"))
                {
                    data.SenderName = ExtractInlineOrNext(line, "اسم المرسل", next);
                }
                // اسم المستلم
                else if (ContainsAny(line, "اسم المستلم"))
                {
                    data.RecipientName = ExtractInlineOrNext(line, "اسم المستلم", next);
                }
                // حساب المستلم
                else if (ContainsAny(line, "حساب المستلم"))
                {
                    data.RecipientAccount = ExtractInlineOrNext(line, "حساب المستلم", next);
                }
                // المبلغ
                else if (ContainsAny(line, "المبلغ") && !ContainsAny(line, "تاريخ"))
                {
                    data.Amount = ExtractInlineOrNext(line, "المبلغ", next);
                }
                // تاريخ العملية
                else if (ContainsAny(line, "تاريخ العملية", "تاريخ الدفع", "التاريخ"))
                {
                    data.PaymentDate = ExtractInlineOrNext(line, new[] { "تاريخ العملية", "تاريخ الدفع", "التاريخ" }, next);
                }
                // رقم العملية
                else if (ContainsAny(line, "رقم", "العملية") && data.OperationNumber == null)
                {
                    var numMatch = System.Text.RegularExpressions.Regex.Match(line + " " + next, @"\b\d{5,10}\b");
                    if (numMatch.Success) data.OperationNumber = numMatch.Value;
                }
            }

            return data;
        }

        private static bool ContainsAny(string line, params string[] keywords)
            => keywords.Any(k => line.Contains(k, StringComparison.OrdinalIgnoreCase));

        private static string? ExtractInlineOrNext(string line, string label, string nextLine)
            => ExtractInlineOrNext(line, new[] { label }, nextLine);

        private static string? ExtractInlineOrNext(string line, string[] labels, string nextLine)
        {
            // Try to find value after the label on the same line
            foreach (var label in labels)
            {
                var idx = line.IndexOf(label, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var after = line.Substring(idx + label.Length).TrimStart(':', ' ', '\t');
                    if (!string.IsNullOrWhiteSpace(after))
                        return after.Trim();
                }
            }
            // Fall back to next line if value not inline
            if (!string.IsNullOrWhiteSpace(nextLine) && !ContainsAny(nextLine, "اسم", "حساب", "مبلغ", "تاريخ", "رقم"))
                return nextLine.Trim();

            return null;
        }
    }
}
