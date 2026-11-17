using System;

namespace ObenseuerOS
{
    internal static partial class LottoScratch
    {
        internal static void Record(LottoSave state, ScratchRec rec, Action<ScratchRec> append, Func<bool> refund)
        {
            try { append(rec); }
            catch (Exception)
            {
                if (state.Cards.Contains(rec)) { state.NextCard = rec.Id; if (rec.Day == state.ScratchDay) state.ScratchToday++; throw; }
                if (rec.Shipped) throw new OsBridgeException("failed", "The card was shipped, but its lottery record could not be saved. Check Shop order history.");
                bool returned = refund();
                throw new OsBridgeException("failed", returned ? "The card could not be recorded. Your money was refunded." : "The card could not be recorded. Your money is being returned.");
            }
            state.NextCard = rec.Id;
            if (rec.Day == state.ScratchDay) state.ScratchToday++;
        }
    }
}
