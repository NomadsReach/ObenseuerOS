using System;
using System.Collections.Generic;
using ObenseuerOS;

namespace ObenseuerOS
{
    internal sealed class OsBridgeException : Exception { internal OsBridgeException(string code, string message) : base(message) { } }
    internal sealed class BetSave { internal readonly List<BetRec> Bets = new List<BetRec>(); internal readonly List<PendingRec> Pending = new List<PendingRec>(); internal int NextBet; }
    internal sealed class BetRec { internal int N; internal string State; }
    internal sealed class PendingRec { internal int Oc; }
    internal static partial class BetBook { internal const string Won = "won"; }
}
internal static class Program
{
    private static void Check(bool value) { if (!value) throw new Exception("Bet record check failed"); }
    private static void Main()
    {
        BetSave state = new BetSave(); BetRec bet = new BetRec { N = 1, State = "open" }; int refunds = 0;
        try { BetBook.Record(state, bet, value => { throw new InvalidOperationException(); }, () => { refunds++; return true; }); throw new Exception("expected failure"); }
        catch (OsBridgeException e) { Check(e.Message.Contains("refunded")); }
        Check(refunds == 1 && state.Bets.Count == 0 && state.NextBet == 0);
        try { BetBook.Record(state, bet, value => { throw new InvalidOperationException(); }, () => { refunds++; return false; }); throw new Exception("expected failure"); }
        catch (OsBridgeException e) { Check(e.Message.Contains("being returned")); }
        Check(refunds == 2 && state.Bets.Count == 0);
        try { BetBook.Record(state, bet, value => { state.Bets.Add(value); throw new InvalidOperationException(); }, () => { refunds++; return true; }); throw new Exception("expected failure"); }
        catch (InvalidOperationException) { }
        Check(refunds == 2 && state.Bets.Count == 1 && state.NextBet == 1);
        PendingRec owed = new PendingRec { Oc = 20 };
        try { BetBook.QueueWin(state, bet, owed, value => { throw new InvalidOperationException(); }); throw new Exception("expected failure"); }
        catch (InvalidOperationException) { }
        Check(bet.State == "open" && state.Pending.Count == 0);
        try { BetBook.QueueWin(state, bet, owed, value => { state.Pending.Add(value); throw new InvalidOperationException(); }); throw new Exception("expected failure"); }
        catch (InvalidOperationException) { }
        Check(bet.State == "won" && state.Pending.Count == 1);
        BetSave normal = new BetSave(); BetRec placed = new BetRec { N = 1, State = "open" }; PendingRec payout = new PendingRec { Oc = 25 };
        BetBook.Record(normal, placed, normal.Bets.Add, () => { throw new Exception("unexpected refund"); });
        BetBook.QueueWin(normal, placed, payout, normal.Pending.Add);
        Check(normal.NextBet == 1 && normal.Bets.Count == 1 && placed.State == "won" && normal.Pending.Count == 1);
        Console.WriteLine("Bet record checks passed");
    }
}
