using System.Collections.Generic;
using Newtonsoft.Json;

namespace SystemColors
{
    public class DisciplineEntry
    {
        [JsonProperty("codes")]
        public List<string> Codes { get; set; } = new List<string>();

        [JsonProperty("color")]
        public int[] Color { get; set; } = new int[3];
    }

    public class SystemEntry
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("keywords")]
        public List<string> Keywords { get; set; } = new List<string>();

        [JsonProperty("color")]
        public int[] Color { get; set; } = new int[3];

        /// <summary>
        /// Optional transparency 0.0 (opaque) to 1.0 (fully transparent). 0 = no transparency override.
        /// </summary>
        [JsonProperty("transparency")]
        public double Transparency { get; set; } = 0.0;
    }

    public class ColorConfig
    {
        /// <summary>
        /// Property names to search in order. First non-empty value found is used for system matching.
        /// </summary>
        [JsonProperty("systemPropertyNames")]
        public List<string> SystemPropertyNames { get; set; } = new List<string> { "Fabrication Service" };

        /// <summary>
        /// Selection tree group names that identify insulation elements.
        /// </summary>
        [JsonProperty("insulationGroupNames")]
        public List<string> InsulationGroupNames { get; set; } = new List<string> { "Insulation" };

        [JsonProperty("insulationTransparency")]
        public double InsulationTransparency { get; set; } = 0.5;

        [JsonProperty("disciplines")]
        public List<DisciplineEntry> Disciplines { get; set; } = new List<DisciplineEntry>();

        [JsonProperty("systems")]
        public List<SystemEntry> Systems { get; set; } = new List<SystemEntry>();

        public static ColorConfig LoadFrom(string jsonPath)
        {
            var json = System.IO.File.ReadAllText(jsonPath);
            return JsonConvert.DeserializeObject<ColorConfig>(json);
        }
    }
}
