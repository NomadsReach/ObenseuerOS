namespace ObenseuerOS
{
    internal enum TaskbarAction { Open, Restore, Focus, Minimize }
    internal static class ToolkitTaskbarState
    {
        internal static bool ShowPinnedLauncher(bool native, bool windowOpen)
        {
            return !native || !windowOpen;
        }
        internal static TaskbarAction Decide(bool visible, bool minimized, bool focused)
        {
            if (minimized) return TaskbarAction.Restore;
            if (!visible) return TaskbarAction.Open;
            return focused ? TaskbarAction.Minimize : TaskbarAction.Focus;
        }
    }
}
