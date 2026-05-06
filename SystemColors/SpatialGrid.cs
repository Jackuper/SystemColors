using System;
using System.Collections.Generic;

namespace SystemColors
{
    /// <summary>
    /// Buckets 3D anchor points into a grid for fast nearest-neighbor lookup.
    /// Searches the anchor's own cell plus the 26 adjacent cells (3x3x3 neighborhood).
    /// </summary>
    public class SpatialGrid
    {
        private readonly double _cellSize;
        private readonly Dictionary<(int, int, int), List<(double X, double Y, double Z, RgbColor Color)>> _cells
            = new Dictionary<(int, int, int), List<(double X, double Y, double Z, RgbColor Color)>>();

        public SpatialGrid(double cellSize)
        {
            if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
            _cellSize = cellSize;
        }

        public void AddAnchor(double x, double y, double z, RgbColor color)
        {
            var key = CellKey(x, y, z);
            if (!_cells.TryGetValue(key, out var list))
            {
                list = new List<(double X, double Y, double Z, RgbColor Color)>();
                _cells[key] = list;
            }
            list.Add((x, y, z, color));
        }

        public RgbColor? FindNearest(double x, double y, double z)
        {
            var (cx, cy, cz) = CellKey(x, y, z);
            double bestSq = double.MaxValue;
            RgbColor? bestColor = null;

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!_cells.TryGetValue((cx + dx, cy + dy, cz + dz), out var list)) continue;
                foreach (var (ax, ay, az, color) in list)
                {
                    var sq = (ax - x) * (ax - x) + (ay - y) * (ay - y) + (az - z) * (az - z);
                    if (sq < bestSq) { bestSq = sq; bestColor = color; }
                }
            }
            return bestColor;
        }

        private (int, int, int) CellKey(double x, double y, double z) =>
            ((int)Math.Floor(x / _cellSize),
             (int)Math.Floor(y / _cellSize),
             (int)Math.Floor(z / _cellSize));
    }
}
