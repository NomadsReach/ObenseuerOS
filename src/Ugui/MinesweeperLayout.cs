using System;

namespace ObenseuerOS
{
    internal static class MinesweeperLayout
    {
        internal static int CellSize(float width, float height, int columns, int rows)
        {
            if (width <= 0 || height <= 0 || float.IsNaN(width) || float.IsNaN(height)) return 16;
            return Math.Max(16, (int)Math.Floor(Math.Min(width / columns, height / rows)));
        }
    }
}
