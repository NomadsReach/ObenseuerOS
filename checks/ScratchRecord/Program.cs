using System;
using ObenseuerOS;

namespace ObenseuerOS
{
    internal sealed class OsBridgeException : Exception { internal OsBridgeException(string code, string message) : base(message) { } }
}
internal static class Program
{
    private static void Check(bool ok) { if (!ok) throw new Exception("Scratch record check failed"); }
    private static void Main()
    {
        LottoSave state = new LottoSave(); ScratchRec card = new ScratchRec { Id = 1, Paid = 45 }; int refunds = 0;
        try { LottoScratch.Record(state, card, value => { throw new InvalidOperationException(); }, () => { refunds++; return true; }); throw new Exception("expected failure"); }
        catch (OsBridgeException e) { Check(e.Message.Contains("refunded")); }
        Check(refunds == 1 && state.Cards.Count == 0 && state.NextCard == 0 && state.ScratchToday == 0);
        try { LottoScratch.Record(state, card, value => { throw new InvalidOperationException(); }, () => { refunds++; return false; }); throw new Exception("expected failure"); }
        catch (OsBridgeException e) { Check(e.Message.Contains("being returned")); }
        Check(refunds == 2 && state.Cards.Count == 0);
        try { LottoScratch.Record(state, card, value => { state.Cards.Add(value); throw new InvalidOperationException(); }, () => { refunds++; return true; }); throw new Exception("expected failure"); }
        catch (InvalidOperationException) { }
        Check(refunds == 2 && state.Cards.Count == 1 && state.NextCard == 1 && state.ScratchToday == 1);
        ScratchRec shipped = new ScratchRec { Id = 2, Shipped = true };
        try { LottoScratch.Record(state, shipped, value => { throw new InvalidOperationException(); }, null); throw new Exception("expected failure"); }
        catch (OsBridgeException e) { Check(e.Message.Contains("shipped")); }
        Check(state.Cards.Count == 1 && state.NextCard == 1 && state.ScratchToday == 1);
        state.ScratchDay = 3;
        ScratchRec older = new ScratchRec { Id = 2, Day = 2, Shipped = true };
        LottoScratch.Record(state, older, state.Cards.Add, null);
        Check(state.Cards.Count == 2 && state.NextCard == 2 && state.ScratchToday == 1);
        ShopGates.Current.Orders.Add(new OrderRec { Cid = "recovery", Print = "oc:ship", PaidOc = 55, PlacedMin = 2 * LottoGame.MinutesPerDay });
        state.ScratchDay = 7; state.ScratchToday = LottoScratch.MaxPerDay;
        ScratchRec recovered = LottoScratch.Buy(state, LottoScratch.Oc, true, "recovery", "oc:ship", 7);
        Check(recovered.Shipped && recovered.Day == 2 && recovered.Paid == 55 && state.ScratchToday == LottoScratch.MaxPerDay);
        try { LottoScratch.Buy(state, LottoScratch.Oc, false, "recovery", "oc:wallet", 7); throw new Exception("expected conflict"); }
        catch (OsBridgeException) { }
        LottoSave crowded = new LottoSave { NextCard = 25, ScratchDay = 7 };
        for (int i = 1; i <= 25; i++) crowded.Cards.Add(new ScratchRec { Id = i, Shipped = true });
        LottoScratch.Buy(crowded, LottoScratch.Oc, true, "recovery", "oc:ship", 7);
        Check(crowded.Cards.Count == LottoScratch.MaxHistory && crowded.NextCard == 26);
        Console.WriteLine("Scratch recording checks passed");
    }
}
