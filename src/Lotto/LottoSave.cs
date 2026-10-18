using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    [Serializable]
    public class LottoSave
    {
        public int Seed;
        public int NextDraw;
        public int NextTicket;
        public List<LottoTicket> Tickets = new List<LottoTicket>();
        public List<DrawRec> Draws = new List<DrawRec>();
        public List<OwedRec> Owed = new List<OwedRec>();
        public List<PurchaseRec> Purchases = new List<PurchaseRec>();
        public int ScratchDay;
        public int ScratchToday;
        public int NextCard;
        public List<ScratchRec> Cards = new List<ScratchRec>();
    }

    [Serializable]
    public class LottoTicket
    {
        public int Id;
        public int Draw;
        public List<int> Nums = new List<int>();
        public string State;
        public int Matches;
        public int Oc;
    }

    [Serializable]
    public class DrawRec
    {
        public int N;
        public List<int> Nums = new List<int>();
        public List<int> Winners = new List<int>();
        public int PaidOc;
        public bool Late;
    }

    [Serializable]
    public class OwedRec
    {
        public int Ticket;
        public int Oc;
    }

    [Serializable]
    public class PurchaseRec
    {
        public string Cid;
        public string Print;
        public int Draw;
        public List<int> TicketIds = new List<int>();
        public List<int> Nums = new List<int>();
        public int Oc;
    }

    [Serializable]
    public class ScratchRec
    {
        public int Id;
        public int ItemId;
        public int Day;
        public int Paid;
        public bool Shipped;
        public bool Revealed;
        public int Prize;
        public string Cid;
        public string Print;
    }
}
