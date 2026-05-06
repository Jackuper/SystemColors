using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;

namespace SystemColors
{
    [Plugin("SystemColors.ApplyColors", "SYSCOL",
        DisplayName = "Apply System Colors",
        ToolTip = "Colors model elements by discipline based on filename")]
    [AddInPluginAttribute(AddInLocation.AddIn)]
    public class SystemColorsPlugin : AddInPlugin
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
                        "System Colors",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning
                    );
                    return 1;
                }

                var config = ColorConfig.LoadFrom(configPath);
                var mapper = new DisciplineColorMapper(config);
                var doc = Application.ActiveDocument;

                ApplyColors(doc, mapper, config);

                return 0;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Error applying colors:\n{ex.Message}",
                    "System Colors",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error
                );
                return 1;
            }
        }

        private void ApplyColors(Document doc, DisciplineColorMapper mapper, ColorConfig config)
        {
            int colored = 0;
            int skipped = 0;

            foreach (var fileNode in doc.Models.RootItems)
            {
                var fileName = Path.GetFileName(fileNode.Model?.FileName ?? string.Empty);
                if (string.IsNullOrEmpty(fileName)) continue;

                var disciplineRgb = mapper.GetColor(fileName);

                var allItems = new ModelItemCollection();
                var systemGroups = new Dictionary<string, ModelItemCollection>(StringComparer.OrdinalIgnoreCase);
                var insulationItems = new ModelItemCollection();

                foreach (var item in fileNode.DescendantsAndSelf)
                {
                    allItems.Add(item);

                    var systemValue = GetSystemPropertyValue(item, config.SystemPropertyNames);
                    if (!string.IsNullOrEmpty(systemValue) && mapper.GetSystemColor(systemValue) != null)
                    {
                        if (!systemGroups.ContainsKey(systemValue))
                            systemGroups[systemValue] = new ModelItemCollection();
                        systemGroups[systemValue].Add(item);
                    }

                    if (config.InsulationGroupNames != null && config.InsulationGroupNames.Count > 0
                        && IsUnderGroup(item, config.InsulationGroupNames))
                        insulationItems.Add(item);
                }

                bool anyColored = false;

                // Reset prior overrides on this file's items so stale colors from
                // earlier runs do not persist when keywords or excluded disciplines change.
                doc.Models.ResetPermanentMaterials(allItems);

                // Pass 1: discipline color for all items in this file
                if (disciplineRgb != null)
                {
                    doc.Models.OverridePermanentColor(allItems, ToNavisColor(disciplineRgb.Value));
                    anyColored = true;
                }

                // Pass 2: system-specific colors (overrides discipline for matched items)
                foreach (var kvp in systemGroups)
                {
                    var systemRgb = mapper.GetSystemColor(kvp.Key);
                    if (systemRgb == null) continue;
                    doc.Models.OverridePermanentColor(kvp.Value, ToNavisColor(systemRgb.Value));
                    anyColored = true;
                }

                // Pass 3: white + transparency for insulation (always runs last)
                if (insulationItems.Count > 0)
                {
                    doc.Models.OverridePermanentColor(insulationItems, new Color(1.0, 1.0, 1.0));
                    doc.Models.OverridePermanentTransparency(insulationItems, config.InsulationTransparency);
                    anyColored = true;
                }

                if (anyColored) colored++;
                else skipped++;
            }

            System.Windows.Forms.MessageBox.Show(
                $"Done! Colored {colored} file(s), skipped {skipped} unrecognized file(s).",
                "System Colors",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information
            );
        }

        /// <summary>
        /// Tries each property name in order and returns the first non-empty value found.
        /// </summary>
        private static string GetSystemPropertyValue(ModelItem item, IList<string> propertyNames)
        {
            if (propertyNames == null) return null;
            foreach (var propName in propertyNames)
            {
                var value = GetPropertyValue(item, propName);
                if (!string.IsNullOrEmpty(value)) return value;
            }
            return null;
        }

        private static bool IsUnderGroup(ModelItem item, IList<string> groupNames)
        {
            foreach (var ancestor in item.AncestorsAndSelf)
                foreach (var name in groupNames)
                    if (ancestor.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return true;
            return false;
        }

        private static Color ToNavisColor(RgbColor rgb)
        {
            return new Color(rgb.R / 255.0, rgb.G / 255.0, rgb.B / 255.0);
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
