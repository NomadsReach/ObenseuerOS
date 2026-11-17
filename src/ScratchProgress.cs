using System.Collections.Generic;

namespace ObenseuerOS
{
    internal sealed class ScratchProgress
    {
        private readonly HashSet<int> cleared = new HashSet<int>();
        internal bool Mark(int index) { return cleared.Add(index); }
        internal bool Finished { get { return cleared.Count >= 15; } }
    }
}
