using System.Collections.Generic;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class OsDesktopState
    {
        private static readonly NoticeQueue notices = new NoticeQueue();

        internal static string UserName() { return (PlayerState.FirstName + " " + PlayerState.LastName).Trim(); }
        internal static bool AddNotice(OsNotice notice) { return notices.Add(notice); }
        internal static List<OsNotice> TakeNotices() { return notices.Take(); }
        internal static void ClearNotices(string modId) { notices.RemoveMod(modId); }
        internal static void Reset(bool mainMenu) { if (mainMenu) notices.Clear(); }
    }
}
