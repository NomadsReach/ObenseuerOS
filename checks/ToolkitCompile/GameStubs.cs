using System;
using System.Collections.Generic;
using UnityEngine;

namespace ObenseuerCore
{
    internal enum MoneyCurrency { OC, RM }
    internal static class Cash
    {
        internal static bool IsAvailable { get { return false; } }
        internal static decimal Balance(MoneyCurrency currency) { return 0; }
    }
    internal struct WorldTime { internal int CalendarDay, Hour, Minute; internal DayOfWeek Weekday; }
    internal static class WorldClock
    {
        internal static bool IsAvailable { get { return false; } }
        internal static WorldTime Now { get { return new WorldTime(); } }
    }
    internal static class ComputerApps
    {
        internal static void SetCloseGuard(Transform host, Func<bool> close) { }
        internal static void Close(string id) { }
    }
}

namespace ObenseuerOS
{
    internal sealed class OsNotepadStorage : NotepadStorage
    {
        internal void CheckSession() { }
        public string[] List() { return null; }
        public string Read(string path) { return null; }
        public void Write(string path, string value) { }
        public bool Valid(string path) { return false; }
        public void Opened(string path) { }
    }
    internal static class OsView { internal static string UserName() { return ""; } }
    internal sealed class ChatRoomDef { internal string Id; }
    internal static class ChatData
    {
        internal const int MaxText = 200;
        internal static readonly List<ChatRoomDef> Rooms = new List<ChatRoomDef>();
        internal static ChatRoomDef Find(string id) { return null; }
    }
    internal static class ChatBridge
    {
        internal static string Since(string args) { return ""; }
        internal static string Send(string args) { return ""; }
    }
}
