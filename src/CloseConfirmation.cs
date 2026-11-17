using System;

namespace ObenseuerOS
{
    internal sealed class CloseConfirmation
    {
        private object revision;
        private bool armed, hasRevision;

        internal bool Allow(bool dirty, object currentRevision)
        {
            if (!dirty) { Reset(); return true; }
            if (!hasRevision || !object.Equals(revision, currentRevision))
            {
                revision = currentRevision; hasRevision = true; armed = false;
            }
            if (armed) return true;
            armed = true; return false;
        }

        internal void Reset() { revision = null; armed = false; hasRevision = false; }
    }
}
