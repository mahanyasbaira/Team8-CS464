using System;
using System.Collections.Generic;

namespace Team8.Core
{
    // Counts backtracking as re-entries into floor grid cells that were already visited.
    // Same rules as analysis/backtrack.py, keep them in sync (docs/PLAN.md section 2.4):
    //  - cell = floor(x / size), floor(z / size)
    //  - the current cell only changes once the position is more than `hysteresis` metres
    //    past the edge of the current cell (stops boundary flicker from head sway)
    //  - staying in the same cell never counts, entering an already visited cell counts 1
    public class GridBacktrackCounter
    {
        public double CellSize { get; }
        public double Hysteresis { get; }

        public int Backtracks { get; private set; }
        public int CellsEntered { get; private set; }
        public int UniqueCells => visited.Count;

        public bool HasCell { get; private set; }
        public int CellX { get; private set; }
        public int CellZ { get; private set; }

        readonly HashSet<long> visited = new HashSet<long>();

        public GridBacktrackCounter(double cellSize = 1.0, double hysteresis = 0.15)
        {
            if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (hysteresis < 0 || hysteresis >= cellSize / 2) throw new ArgumentOutOfRangeException(nameof(hysteresis));
            CellSize = cellSize;
            Hysteresis = hysteresis;
        }

        public static int CellOf(double v, double size)
        {
            return (int)Math.Floor(v / size);
        }

        public static string CellId(int cx, int cz)
        {
            return cx + "_" + cz;
        }

        public void Reset()
        {
            visited.Clear();
            Backtracks = 0;
            CellsEntered = 0;
            HasCell = false;
        }

        // Feed one position sample. Returns true if this sample was a re-entry.
        public bool AddSample(double x, double z)
        {
            int cx = CellOf(x, CellSize);
            int cz = CellOf(z, CellSize);

            if (!HasCell)
            {
                Enter(cx, cz);
                return false;
            }

            if (cx == CellX && cz == CellZ) return false;
            if (DistanceOutsideCurrentCell(x, z) <= Hysteresis) return false;

            return Enter(cx, cz);
        }

        bool Enter(int cx, int cz)
        {
            HasCell = true;
            CellX = cx;
            CellZ = cz;
            CellsEntered++;
            bool isNew = visited.Add(Key(cx, cz));
            if (!isNew) Backtracks++;
            return !isNew;
        }

        // how far (in metres, Chebyshev) the point is outside the current cell's square
        double DistanceOutsideCurrentCell(double x, double z)
        {
            double minX = CellX * CellSize, maxX = minX + CellSize;
            double minZ = CellZ * CellSize, maxZ = minZ + CellSize;
            double dx = Math.Max(Math.Max(minX - x, x - maxX), 0);
            double dz = Math.Max(Math.Max(minZ - z, z - maxZ), 0);
            return Math.Max(dx, dz);
        }

        static long Key(int cx, int cz)
        {
            return ((long)cx << 32) ^ (uint)cz;
        }
    }
}
