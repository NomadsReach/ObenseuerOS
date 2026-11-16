using System;

namespace ObenseuerOS
{
    internal static class BankQuote
    {
        internal static bool TryCreate(string currency, decimal amount, decimal percent, out decimal debit, out decimal fee, out decimal sent)
        {
            debit = fee = sent = 0m;
            if ((currency != "OC" && currency != "RM") || amount <= 0m || amount > 16000000m || percent < 0m || percent > 20m) return false;
            decimal unit = currency == "RM" ? 0.1m : 1m;
            debit = currency == "RM" ? Math.Round(amount, 1) : Math.Floor(amount);
            if (debit <= 0m) return false;
            fee = Math.Max(unit, Math.Ceiling(debit * percent / 100m / unit) * unit);
            sent = debit - fee;
            return sent >= unit;
        }
    }
}
