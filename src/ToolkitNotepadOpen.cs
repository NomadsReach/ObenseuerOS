using System;

namespace ObenseuerOS
{
    internal enum ExternalOpenAction { Focus, Load, Confirm }
    internal static class ToolkitNotepadOpen
    {
        internal static ExternalOpenAction Decide(string current, string requested, bool dirty)
        {
            if (string.Equals(current, requested, StringComparison.Ordinal)) return ExternalOpenAction.Focus;
            return dirty ? ExternalOpenAction.Confirm : ExternalOpenAction.Load;
        }
    }
}
