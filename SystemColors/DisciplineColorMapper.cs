using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SystemColors
{
    public struct RgbColor
    {
        public byte R, G, B;
        public RgbColor(byte r, byte g, byte b) { R = r; G = g; B = b; }
    }

    public class SystemMatch
    {
        public string Name { get; }
        public RgbColor Color { get; }
        public double Transparency { get; }
        public SystemMatch(string name, RgbColor color, double transparency)
        {
            Name = name;
            Color = color;
            Transparency = transparency;
        }
    }

    public class DisciplineColorMapper
    {
        private readonly List<(List<string> Codes, RgbColor Color)> _entries;
        private readonly List<(string Name, List<Regex> Patterns, RgbColor Color, double Transparency)> _systemEntries;

        public DisciplineColorMapper(ColorConfig config)
        {
            _entries = new List<(List<string>, RgbColor)>();
            foreach (var d in config.Disciplines)
            {
                _entries.Add((d.Codes, new RgbColor(
                    (byte)d.Color[0], (byte)d.Color[1], (byte)d.Color[2])));
            }

            _systemEntries = new List<(string, List<Regex>, RgbColor, double)>();
            foreach (var s in config.Systems)
            {
                var color = new RgbColor((byte)s.Color[0], (byte)s.Color[1], (byte)s.Color[2]);
                // Use Keywords list; fall back to Name as the single keyword for backward compatibility
                var keywords = s.Keywords != null && s.Keywords.Count > 0
                    ? s.Keywords
                    : new List<string> { s.Name };
                var patterns = new List<Regex>();
                foreach (var kw in keywords)
                {
                    if (string.IsNullOrWhiteSpace(kw)) continue;
                    patterns.Add(BuildKeywordRegex(kw));
                }
                if (patterns.Count > 0)
                    _systemEntries.Add((s.Name, patterns, color, s.Transparency));
            }
        }

        // Word-boundary match prevents short keywords like "RA" from matching
        // inside longer words (e.g. "structuRAl"). Space / hyphen / underscore
        // are treated as interchangeable separators so a keyword written
        // "Storm Drain" still matches a fab value like "01-Storm-Drain".
        private static Regex BuildKeywordRegex(string keyword)
        {
            var parts = Regex.Split(keyword, @"[\s\-_]+");
            var escapedParts = new List<string>();
            foreach (var p in parts)
                if (p.Length > 0) escapedParts.Add(Regex.Escape(p));
            var body = string.Join(@"[\s\-_]+", escapedParts);
            string left  = IsWordChar(keyword[0])                ? @"\b" : string.Empty;
            string right = IsWordChar(keyword[keyword.Length-1]) ? @"\b" : string.Empty;
            return new Regex(left + body + right, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static bool IsWordChar(char c) =>
            char.IsLetterOrDigit(c) || c == '_';

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
            return GetSystemMatch(propertyValue)?.Color;
        }

        /// <summary>
        /// Returns the matching entry's color and transparency, or null if no match.
        /// </summary>
        public SystemMatch GetSystemMatch(string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue)) return null;

            foreach (var entry in _systemEntries)
                foreach (var pattern in entry.Patterns)
                    if (pattern.IsMatch(propertyValue))
                        return new SystemMatch(entry.Name, entry.Color, entry.Transparency);

            return null;
        }
    }
}
