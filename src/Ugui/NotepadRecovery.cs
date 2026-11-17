using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal sealed class RecoveredNote
    {
        internal readonly string Owner, Origin, Text;
        internal RecoveredNote(string owner, string origin, string text) { Owner = owner; Origin = origin; Text = text; }
    }

    internal static class NotepadRecovery
    {
        private static readonly List<RecoveredNote> notes = new List<RecoveredNote>();

        internal static RecoveredNote Retain(string owner, string origin, string text, string saved)
        {
            if (string.IsNullOrEmpty(owner) || string.Equals(text, saved, StringComparison.Ordinal)) return null;
            RecoveredNote note = new RecoveredNote(owner, origin, text);
            notes.Add(note); return note;
        }

        internal static void Remove(RecoveredNote note) { if (note != null) notes.Remove(note); }
        internal static List<RecoveredNote> List() { return new List<RecoveredNote>(notes); }
        internal static List<RecoveredNote> List(string owner, bool otherSessions)
        {
            return notes.FindAll(note => string.Equals(note.Owner, owner, StringComparison.OrdinalIgnoreCase) != otherSessions);
        }
        internal static bool Claim(RecoveredNote note, string owner, bool otherSessions)
        {
            return note != null && (string.Equals(note.Owner, owner, StringComparison.OrdinalIgnoreCase) != otherSessions) && notes.Remove(note);
        }
    }
}
