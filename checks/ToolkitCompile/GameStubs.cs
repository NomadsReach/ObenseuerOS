using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json.Linq;

namespace ObenseuerCore.Ui
{
    internal static class UiEvents { internal static void Acquire() { } internal static void Release() { } }
}

namespace ObenseuerCore
{
    internal sealed class CoreLog
    {
        internal static CoreLog For(string owner) { return new CoreLog(); }
        internal void Warn(string message) { }
        internal void Error(string message) { }
        internal void Info(string message) { }
        internal void Debug(string key, string message) { }
    }
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
        internal static RectTransform ActiveScreen { get { return null; } }
        internal static void SetCloseGuard(Transform host, Func<bool> close) { }
        internal static void Close(string id) { }
    }
}

namespace ObenseuerOS
{
    internal enum OsResult { Ok, Queued, Unavailable }
    internal sealed class OsApp { internal string Id, ModId, Title, Icon; internal bool Site, Desktop, Pinned; internal int Width, Height, Badge; internal Action OnOpen; internal Action<VisualElement> BuildUi; internal Func<bool> CanClose; internal Action<string, string> OnEvent; }
    internal static class Os { internal const string ModId = "os"; internal static readonly List<OsApp> Apps = new List<OsApp>(); internal static void Opened(string id) { } internal static void Closed(string id) { } internal static void SyncWindow() { } internal static void Crash(string where, Exception error) { } }
    internal sealed class VirusPulse { internal int Popups; internal bool Error, Nag; }
    internal static class VirusFx { internal static event Action<VirusPulse> Pulsed { add { } remove { } } }
    internal static class WikiBridge { internal static string LegacyPages { get { return ""; } } internal static string BundledPages { get { return ""; } } }
    internal static class VirusAv { internal static bool Shows(string appId) { return false; } }
    internal static class OsBridge { internal static JObject Reply(JObject request) { return null; } }
    internal static class OsWallpaper { internal static int Capture(RectTransform screen, string folder) { return -1; } }
    internal static class OsGameArt
    {
        internal sealed class Entry { internal string Id, Name, FilePath; internal int Rev; }
        internal static void Request(string folder) { }
        internal static bool Step() { return false; }
        internal static List<Entry> Ready() { return new List<Entry>(); }
    }
    internal sealed class OsNotepadStorage : NotepadStorage
    {
        internal int SessionId { get { return 0; } }
        internal string RecoveryKey { get { return "save:test"; } }
        internal string RecoveryOrigin { get { return "test"; } }
        internal bool SessionCurrent { get { return true; } }
        internal void CheckSession() { }
        public string[] List() { return null; }
        public string Read(string path) { return null; }
        public void Write(string path, string value) { }
        public bool Valid(string path) { return false; }
        public void Opened(string path) { }
    }
    internal sealed class OsNotice { internal string From, Title, Body; }
    internal static class OsDesktopState { internal static string UserName() { return ""; } internal static List<OsNotice> TakeNotices() { return new List<OsNotice>(); } }
    internal static class OsView { internal static bool Visible { get { return false; } } internal static bool Show(RectTransform screen) { return false; } internal static void Dismiss() { } }
    internal static class OsLaptopPrompt { internal static bool IsLaptopName(string name) { return false; } }
    internal sealed class ChatRoomDef { internal string Id, Name; internal bool Contact; }
    internal static class ChatGame
    {
        internal static ChatRoom Open(string id) { return null; }
        internal static int Unread(string id) { return 0; }
    }
    internal static class ChatData
    {
        internal const int MaxText = 200;
        internal static readonly List<ChatRoomDef> Rooms = new List<ChatRoomDef>();
        internal static ChatRoomDef Find(string id) { return null; }
    }
    internal static class ChatBridge
    {
        internal static string Send(string args) { return ""; }
    }
}

internal sealed class GameController
{
    internal static GameController instance;
    internal void ControlsDisabled(GameObject owner, bool ShowCursor) { }
    internal void ControlsEnabled(GameObject owner) { }
}
internal sealed class ThirdPersonCameraController
{
    internal static ThirdPersonCameraController instance;
    internal Transform FollowTarget;
    internal bool followPlayerDisabled;
    internal void DisableRotation() { }
    internal void EnableRotation() { }
}
internal sealed class ThirdPersonCameraCollision { internal static ThirdPersonCameraCollision instance; internal bool disableCollisionUpdate; }
internal sealed class OpenSewerCharacterController { internal static OpenSewerCharacterController instance; }
internal sealed class InputManager
{
    internal enum InputType { Keyboard }
    internal static InputManager instance;
    internal InputType inputType;
    internal bool GetKeyDown(string key) { return false; }
}
