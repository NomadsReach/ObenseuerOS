using System;
using System.Globalization;
using System.Text.RegularExpressions;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal sealed class Funds
    {
        private const int ChunkCurrency = 1000000;
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        public readonly MoneyCurrency Currency;
        public readonly int Scale;

        public Funds(MoneyCurrency currency)
        {
            Currency = currency;
            Scale = currency == MoneyCurrency.RM ? 10 : 1;
        }

        public string Code
        {
            get { return Currency.ToString(); }
        }

        public string Suffix
        {
            get { return " " + Code; }
        }

        public int Balance
        {
            get { return (int)Math.Min(int.MaxValue, Math.Round(Cash.Balance(Currency) * (double)Scale)); }
        }

        public static MoneyCurrency Parse(string code)
        {
            return code == "RM" ? MoneyCurrency.RM : MoneyCurrency.OC;
        }

        public static string CurrencyIn(string text)
        {
            Match match = Regex.Match(text ?? string.Empty, @"^\s*Currency\s*=\s*(RM|OC)\s*$", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value : null;
        }

        public static float ToCurrency(int units, int scale)
        {
            return (float)((double)units / scale);
        }

        public static int ToUnits(float amount, int scale)
        {
            return (int)Math.Round((double)amount * scale);
        }

        public bool Give(int units)
        {
            return Move(units, true);
        }

        public bool Spend(int units)
        {
            return Move(units, false);
        }

        public string Number(int units)
        {
            return ((decimal)units / Scale).ToString(Scale == 1 ? "#,0" : "#,0.0", CultureInfo.InvariantCulture);
        }

        public string Text(int units)
        {
            return Number(units) + Suffix;
        }

        public string Signed(int units)
        {
            return (units >= 0 ? "+" : "") + Text(units);
        }

        private bool Move(int units, bool credit)
        {
            int step = ChunkCurrency * Scale;
            int done = 0;
            while (done < units)
            {
                int part = Math.Min(units - done, step);
                if (!Part(part, credit)) break;
                done += part;
            }
            if (done == units) return true;
            int undone = 0;
            while (undone < done)
            {
                int part = Math.Min(done - undone, step);
                if (!Part(part, !credit))
                {
                    Log.Error("could not undo a partial " + (credit ? "credit" : "debit") + " of " + Text(done - undone));
                    break;
                }
                undone += part;
            }
            return false;
        }

        private bool Part(int units, bool credit)
        {
            float amount = ToCurrency(units, Scale);
            return credit ? Cash.Give(Currency, amount) : Cash.TrySpend(Currency, amount);
        }
    }
}
