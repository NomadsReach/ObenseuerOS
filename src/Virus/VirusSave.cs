using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    [Serializable]
    public class VirusSave
    {
        public int Seed;
        public List<string> Active = new List<string>();
        public bool RealOwned;
        public bool CheapOwned;
        public bool CheapLapsed;
        public int LastRandomDay = -1;
        public int SpamDay = -1;
        public int CapDay = -1;
        public int DayOc;
        public int DayRm;
        public int CapWeek = -1;
        public int WeekOc;
        public int WeekRm;
        public int KassaDay = -1;
        public List<VirusStolen> Stolen = new List<VirusStolen>();
        public List<VirusMove> Moves = new List<VirusMove>();
        public List<VirusJunk> Junk = new List<VirusJunk>();
        public int VaultSeq;
        public int JunkSeq;
        public int RenameDay = -1;
        public int RenamesToday;
        public int LockDay = -1;
        public int Blocked;
        public int ScanDay = -1;
        public List<VirusReturn> Returning = new List<VirusReturn>();
        public int CheapNextBill = -1;
        public int CheapLapsedSince = -1;
        public int CheapScanAt = -1;
        public bool CheapEnded;
        public List<string> Paid = new List<string>();
    }

    [Serializable]
    public class VirusReturn
    {
        public string Id;
        public int Day;
    }

    [Serializable]
    public class VirusStolen
    {
        public string Id;
        public int Oc;
        public int Rm;
        public int ReceiptDay = -1;
    }

    [Serializable]
    public class VirusMove
    {
        public string Kind;
        public string Original;
        public string Now;
    }

    [Serializable]
    public class VirusJunk
    {
        public string Path;
        public string Text;
    }
}
