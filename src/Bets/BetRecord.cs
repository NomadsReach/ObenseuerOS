using System;

namespace ObenseuerOS
{
    internal static partial class BetBook
    {
        internal static void Record(BetSave state, BetRec bet, Action<BetRec> append, Func<bool> refund)
        {
            try { append(bet); }
            catch (Exception)
            {
                if (state.Bets.Contains(bet)) { state.NextBet = bet.N; throw; }
                bool returned = refund();
                throw new OsBridgeException("failed", returned ? "The bet could not be recorded. Your money was refunded." : "The bet could not be recorded. Your money is being returned.");
            }
            state.NextBet = bet.N;
        }

        internal static void QueueWin(BetSave state, BetRec bet, PendingRec owed, Action<PendingRec> append)
        {
            try { append(owed); }
            catch (Exception)
            {
                if (state.Pending.Contains(owed)) bet.State = Won;
                throw;
            }
            bet.State = Won;
        }
    }
}
