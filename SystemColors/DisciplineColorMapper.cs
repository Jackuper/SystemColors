using System;
using System.Collections.Generic;

namespace SystemColors
{
    public struct RgbColor
    {
        public byte R, G, B;
        public RgbColor(byte r, byte g, byte b) { R = r; G = g; B = b; }
    }

    public class DisciplineColorMapper
    {
        private readonly List<(List<string> Codes, RgbColor Color)> _entries;
        private readonly List<(List<string> Keywords, RgbColor Color)> _systemEntries;

        public DisciplineColorMapper(ColorConfig config)
        {
            _entries = new List<(List<string>, RgbColor)>();
            foreach (var d in config.Disciplines)
            {
                _entries.Add((d.Codes, new RgbColor(
                    (byte)d.Color[0], (byte)d.Color[1], (byte)d.Color[2])));
            }

            _systemEntries = new List<(List<string>, RgbColor)>();
            foreach (var s in config.Systems)
            {
                var color = new RgbColor((byte)s.Color[0], (byte)s.Color[1], (byte)s.Color[2]);
                // Use Keywords list; fall back to Name as the single keyword for backward compatibility
                var keywords = s.Keywords != null && s.Keywords.Count > 0
                    ? s.Keywords
                    : new List<string> { s.Name };
                if (keywords.Count > 0)
                    _systemEntries.Add((keywords, color));
            }
        }

        /// <summary>
        /// Returns the discipline color for the given filename, or null if no match.
        /// </summary>
        public RgbColor? GetColor(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            var upper = System.IO.Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
            var parts = upper.Split(new[] { '_', '-', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var entry in _entries)
                foreach (var code in entry.Codes)
                    foreach (var part in parts)
                        if (part == code.ToUpperInvariant())
                            return entry.Color;

            return null;
        }

        /// <summary>
        /// Returns the color for the first system entry whose keyword appears in propertyValue.
        /// Order in colors.json matters — more specific entries should come first.
        /// </summary>
        public RgbColor? GetSystemColor(string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue)) return null;

            foreach (var entry in _systemEntries)
                foreach (var keyword in entry.Keywords)
                    if (propertyValue.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        return entry.Color;

            return null;
        }
    }
}
