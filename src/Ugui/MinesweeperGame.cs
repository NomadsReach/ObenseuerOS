using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal sealed class MinesweeperGame
    {
        internal sealed class Cell
        {
            internal bool Mine;
            internal bool Open;
            internal int Adjacent;
            internal int Mark;
        }

        internal readonly int Rows;
        internal readonly int Columns;
        internal readonly int Mines;
        internal readonly Cell[] Cells;
        internal bool Placed { get; private set; }
        internal bool Won { get; private set; }
        internal bool Lost { get; private set; }
        internal int Opened { get; private set; }
        internal int Exploded { get; private set; }
        private readonly Random random;

        internal MinesweeperGame(int rows, int columns, int mines, Random random)
        {
            if (rows < 3 || columns < 3 || mines < 1 || mines > rows * columns - 9) throw new ArgumentOutOfRangeException();
            if (random == null) throw new ArgumentNullException("random");
            Rows = rows;
            Columns = columns;
            Mines = mines;
            this.random = random;
            Cells = new Cell[rows * columns];
            for (int i = 0; i < Cells.Length; i++) Cells[i] = new Cell();
            Exploded = -1;
        }

        internal List<int> Nearby(int index)
        {
            CheckIndex(index);
            List<int> result = new List<int>();
            int row = index / Columns;
            int column = index % Columns;
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    int r = row + dr;
                    int c = column + dc;
                    if ((dr != 0 || dc != 0) && r >= 0 && r < Rows && c >= 0 && c < Columns) result.Add(r * Columns + c);
                }
            return result;
        }

        internal bool Reveal(int index)
        {
            CheckIndex(index);
            Cell cell = Cells[index];
            if (Won || Lost || cell.Open || cell.Mark == 1) return false;
            if (!Placed) Place(index);
            if (cell.Mine)
            {
                Lost = true;
                Exploded = index;
                return true;
            }
            Stack<int> pending = new Stack<int>();
            pending.Push(index);
            while (pending.Count > 0)
            {
                int current = pending.Pop();
                Cell next = Cells[current];
                if (next.Open || next.Mark == 1) continue;
                next.Open = true;
                next.Mark = 0;
                Opened++;
                if (next.Adjacent == 0)
                    foreach (int neighbor in Nearby(current))
                        if (!Cells[neighbor].Open) pending.Push(neighbor);
            }
            if (Opened == Cells.Length - Mines)
            {
                Won = true;
                foreach (Cell item in Cells) if (item.Mine) item.Mark = 1;
            }
            return true;
        }

        internal bool Toggle(int index)
        {
            CheckIndex(index);
            Cell cell = Cells[index];
            if (Won || Lost || cell.Open) return false;
            cell.Mark = (cell.Mark + 1) % 3;
            return true;
        }

        internal bool Chord(int index)
        {
            CheckIndex(index);
            Cell cell = Cells[index];
            if (Won || Lost || !cell.Open || cell.Adjacent == 0) return false;
            List<int> neighbors = Nearby(index);
            int flags = 0;
            foreach (int neighbor in neighbors) if (Cells[neighbor].Mark == 1) flags++;
            if (flags != cell.Adjacent) return false;
            foreach (int neighbor in neighbors)
                if (!Cells[neighbor].Open && Cells[neighbor].Mark != 1) Reveal(neighbor);
            return true;
        }

        private void Place(int first)
        {
            List<int> excluded = Nearby(first);
            excluded.Add(first);
            List<int> free = new List<int>();
            for (int i = 0; i < Cells.Length; i++) if (!excluded.Contains(i)) free.Add(i);
            for (int i = 0; i < Mines; i++)
            {
                int j = random.Next(i, free.Count);
                int swap = free[i];
                free[i] = free[j];
                free[j] = swap;
                Cells[free[i]].Mine = true;
            }
            for (int i = 0; i < Cells.Length; i++)
                foreach (int neighbor in Nearby(i)) if (Cells[neighbor].Mine) Cells[i].Adjacent++;
            Placed = true;
        }

        private void CheckIndex(int index)
        {
            if (index < 0 || index >= Cells.Length) throw new ArgumentOutOfRangeException("index");
        }
    }
}
