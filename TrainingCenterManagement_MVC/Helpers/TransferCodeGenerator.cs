using System.Security.Cryptography;

namespace TrainingCenterManagement_MVC.Helpers
{
    public static class TransferCodeGenerator
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        static List<string> generatedCodes = new List<string>();
        static TransferCodeGenerator()
        {
            generatedCodes = new List<string>();
            generatedCodes.Add("ZrIgYZRt");
        }
        public static string Generate(int length = 8)
        { 
            string generatedCode;
            do
            {
                generatedCode = RandomNumberGenerator.GetString(Alphabet, length);
            }
            while (generatedCodes.Contains(generatedCode));
            return generatedCode;
        }
    }
}
