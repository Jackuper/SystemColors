using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;

namespace SystemColors
{
    [Plugin("SystemColors.Audit", "SYSAUDIT",
        DisplayName = "Audit System Colors",
        ToolTip = "Reports unmatched property values to help expand colors.json")]
    [AddInPluginAttribute(AddInLocation.AddIn)]
    public class SystemColorsAuditPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                var configPath = Path.Combine(
                    Path.GetDirectoryName(GetType().Assembly.Location),
                    "colors.json"
                );
                if (!File.Exists(configPath))
                {
                    System.Windows.Forms.MessageBox.Show(
                        $"colors.json not found at:\n{configPath}",
                        "System Colors Audit",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
                    return 1;
                }

                var config = ColorConfig.LoadFrom(configPath);
                var mapper = new DisciplineColorMapper(config);
                var doc = Application.ActiveDocument;

                var report = BuildReport(doc, config, mapper);

                var reportPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"SystemColorsAudit_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
                );
                File.WriteAllText(reportPath, report);

                var result = System.Windows.Forms.MessageBox.Show(
                    $"Audit complete.\n\nSaved to:\n{reportPath}\n\nOpen now?",
                    "System Colors Audit",
                    System.Windows.Forms.MessageBoxButtons.YesNo,
                    System.Windows.Forms.MessageBoxIcon.Information);
                if (result == System.Windows.Forms.DialogResult.Yes)
                    System.Diagnostics.Process.Start(reportPath);

                return 0;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Error during audit:\n{ex.Message}",
                    "System Colors Audit",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return 1;
            }
        }

        private static string BuildReport(Document doc, ColorConfig config, DisciplineColorMapper mapper)
        {
            // perProperty[propName][value] = (count, matchedSystemName or null)
            var perProperty = new Dictionary<string, Dictionary<string, ValueStat>>(StringComparer.OrdinalIgnoreCase);
            int totalItems = 0;
            int matchedItems = 0;

            foreach (var fileNode in doc.Models.RootItems)
            {
                foreach (var item in fileNode.DescendantsAndSelf)
                {
                    totalItems++;
                    bool itemMatched = false;
                    foreach (var propName in config.SystemPropertyNames)
                    {
                        var value = GetPropertyValue(item, propName);
                        if (string.IsNullOrEmpty(value)) continue;

                        var match = mapper.GetSystemMatch(value);
                        if (match != null) itemMatched = true;

                        if (!perProperty.TryGetValue(propName, out var byValue))
                        {
                            byValue = new Dictionary<string, ValueStat>(StringComparer.OrdinalIgnoreCase);
                            perProperty[propName] = byValue;
                        }
                        if (!byValue.TryGetValue(value, out var stat))
                        {
                            stat = new ValueStat { MatchedSystem = match?.Name };
                            byValue[value] = stat;
                        }
                        stat.Count++;
                    }
                    if (itemMatched) matchedItems++;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("System Colors Audit Report");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"Total items scanned:     {totalItems:N0}");
            sb.AppendLine($"Items matched (colored): {matchedItems:N0}");
            sb.AppendLine($"Items unmatched:         {totalItems - matchedItems:N0}");
            sb.AppendLine();
            sb.AppendLine("Configured systemPropertyNames (in order):");
            foreach (var p in config.SystemPropertyNames) sb.AppendLine("  " + p);
            sb.AppendLine();

            // === UNMATCHED ===
            sb.AppendLine("================================================================");
            sb.AppendLine("UNMATCHED VALUES");
            sb.AppendLine("Property values found in the model that did not match any keyword.");
            sb.AppendLine("Add keywords to colors.json to color elements with these values.");
            sb.AppendLine("================================================================");
            sb.AppendLine();
            foreach (var propName in config.SystemPropertyNames)
            {
                if (!perProperty.TryGetValue(propName, out var byValue)) continue;
                var unmatched = byValue.Where(v => v.Value.MatchedSystem == null)
                                       .OrderByDescending(v => v.Value.Count)
                                       .ToList();
                if (unmatched.Count == 0) continue;
                sb.AppendLine($"--- {propName} ---");
                foreach (var entry in unmatched)
                    sb.AppendLine($"  {entry.Value.Count,7:N0}  {entry.Key}");
                sb.AppendLine();
            }

            // === MATCHED ===
            sb.AppendLine("================================================================");
            sb.AppendLine("MATCHED VALUES");
            sb.AppendLine("Property values currently mapping to a system color (sanity check).");
            sb.AppendLine("================================================================");
            sb.AppendLine();
            var bySystem = new Dictionary<string, List<(string PropName, string Value, int Count)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var propEntry in perProperty)
            {
                foreach (var v in propEntry.Value)
                {
                    if (v.Value.MatchedSystem == null) continue;
                    if (!bySystem.TryGetValue(v.Value.MatchedSystem, out var list))
                    {
                        list = new List<(string, string, int)>();
                        bySystem[v.Value.MatchedSystem] = list;
                    }
                    list.Add((propEntry.Key, v.Key, v.Value.Count));
                }
            }
            foreach (var sysName in bySystem.Keys.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"--- {sysName} ---");
                foreach (var entry in bySystem[sysName].OrderByDescending(e => e.Count))
                    sb.AppendLine($"  {entry.Count,7:N0}  via {entry.PropName}: {entry.Value}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private class ValueStat
        {
            public int Count;
            public string MatchedSystem;
        }

        private static string GetPropertyValue(ModelItem item, string propertyName)
        {
            foreach (var category in item.PropertyCategories)
                foreach (var prop in category.Properties)
                    if (prop.DisplayName.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var value = prop.Value.ToDisplayString();
                            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
                        }
                        catch { }
                    }
            return null;
        }
    }
}
