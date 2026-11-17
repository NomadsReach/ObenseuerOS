using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal sealed class NativeDesktopClose<T> where T : class
    {
        private bool closing;

        internal bool Dismiss(Func<List<T>> snapshot, Func<T, bool> canClose, Action close)
        {
            if (closing) return true;
            List<T> original = snapshot();
            if (original.Count == 0) return false;
            closing = true;
            try
            {
                foreach (T host in original) if (!canClose(host)) return true;
                List<T> current = snapshot();
                if (current.Count != original.Count) return true;
                for (int i = 0; i < current.Count; i++)
                    if (!object.ReferenceEquals(current[i], original[i])) return true;
                close();
                return true;
            }
            finally { closing = false; }
        }
    }
}
