using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal static class BankInvoicePage
    {
        internal static List<T> Select<T>(IEnumerable<T> invoices, Func<T, int> id, int after, int limit, out int next)
        {
            if (limit < 1) throw new ArgumentOutOfRangeException("limit");
            List<T> ordered = new List<T>(invoices);
            ordered.Sort(delegate(T a, T b) { return id(a).CompareTo(id(b)); });
            List<T> result = new List<T>(); next = 0;
            foreach (T invoice in ordered)
            {
                if (id(invoice) <= after) continue;
                if (result.Count == limit) { next = id(result[result.Count - 1]); break; }
                result.Add(invoice);
            }
            return result;
        }
    }
    internal sealed class BankInvoiceNavigation
    {
        private readonly Stack<int> history = new Stack<int>();
        internal int After { get; private set; }
        internal bool CanGoBack { get { return history.Count > 0; } }
        internal bool Next(int cursor)
        {
            if (cursor <= After) return false;
            history.Push(After); After = cursor; return true;
        }
        internal bool Previous()
        {
            if (history.Count == 0) return false;
            After = history.Pop(); return true;
        }
        internal void Reset() { history.Clear(); After = 0; }
    }

}
