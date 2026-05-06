# Insulation Spatial Coloring Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Color insulation elements in Navisworks by inheriting the system color of their nearest duct element using bounding box spatial proximity.

**Architecture:** Add a `SpatialGrid` class that buckets duct anchors into 3D grid cells. After the existing two-pass coloring, insulation items (detected by a configurable property name) query the grid for their nearest system-colored neighbor and inherit that color. Each item is colored individually since mixed-system insulation groups cannot be batch-colored.

**Tech Stack:** C# .NET Framework 4.8, Navisworks 2026 API, MSTest

---

### Task 1: Add config fields to ColorConfig

**Files:**
- Modify: `SystemColors\SystemColors\ColorConfig.cs`
- Modify: `SystemColors\SystemColors\colors.json`

**Step 1: Add two new properties to `ColorConfig`**

In `ColorConfig.cs`, add after the `SystemPropertyName` property:

```csharp
[JsonProperty("insulationPropertyName")]
public string InsulationPropertyName { get; set; } = "Fabrication Skin";

[JsonProperty("spatialGridSize")]
public double SpatialGridSize { get; set; } = 5.0;
```

**Step 2: Add the fields to colors.json**

Add after `"systemPropertyName"`:

```json
"insulationPropertyName": "Fabrication Skin",
"spatialGridSize": 5.0,
```

**Step 3: Build and verify no compile errors**

```
msbuild SystemColors\SystemColors\SystemColors.csproj /p:Configuration=Debug
```
Expected: Build succeeded, 0 errors.

---

### Task 2: Create SpatialGrid class with tests

**Files:**
- Create: `SystemColors\SystemColors\SpatialGrid.cs`
- Modify: `SystemColors\SystemColors.Tests\DisciplineColorMapperTests.cs` (add new test class at bottom)

**Step 1: Write the failing tests first**

Add this class at the bottom of `DisciplineColorMapperTests.cs` (before the final `}`):

```csharp
[TestClass]
public class SpatialGridTests
{
    [TestMethod]
    public void FindNearest_ReturnsNull_WhenEmpty()
    {
        var grid = new SpatialGrid(5.0);
        Assert.IsNull(grid.FindNearest(0, 0, 0));
    }

    [TestMethod]
    public void FindNearest_ReturnsSingleAnchor()
    {
        var grid = new SpatialGrid(5.0);
        var color = new RgbColor(255, 0, 0);
        grid.AddAnchor(1.0, 2.0, 3.0, color);
        var result = grid.FindNearest(1.0, 2.0, 3.0);
        Assert.IsNotNull(result);
        Assert.AreEqual((byte)255, result.Value.R);
        Assert.AreEqual((byte)0,   result.Value.G);
        Assert.AreEqual((byte)0,   result.Value.B);
    }

    [TestMethod]
    public void FindNearest_ReturnsClosestOfTwo()
    {
        var grid = new SpatialGrid(5.0);
        var red   = new RgbColor(255, 0, 0);
        var blue  = new RgbColor(0, 0, 255);
        grid.AddAnchor(0.0, 0.0, 0.0, red);
        grid.AddAnchor(10.0, 0.0, 0.0, blue);
        // Query near the red anchor
        var result = grid.FindNearest(1.0, 0.0, 0.0);
        Assert.IsNotNull(result);
        Assert.AreEqual((byte)255, result.Value.R);
        Assert.AreEqual((byte)0,   result.Value.B);
    }

    [TestMethod]
    public void FindNearest_WorksAcrossCellBoundary()
    {
        var grid = new SpatialGrid(5.0);
        var color = new RgbColor(0, 255, 0);
        // Anchor is at x=4.9 (cell 0), query is at x=5.1 (cell 1) — adjacent cells
        grid.AddAnchor(4.9, 0.0, 0.0, color);
        var result = grid.FindNearest(5.1, 0.0, 0.0);
        Assert.IsNotNull(result);
        Assert.AreEqual((byte)255, result.Value.G);
    }

    [TestMethod]
    public void FindNearest_ReturnsNull_WhenNearestIsTooFar()
    {
        // Anchor is 100 units away — outside the 3x3x3 neighborhood
        var grid = new SpatialGrid(5.0);
        grid.AddAnchor(100.0, 0.0, 0.0, new RgbColor(255, 0, 0));
        // Nothing within 1 cell (~5 ft) of origin
        var result = grid.FindNearest(0.0, 0.0, 0.0);
        Assert.IsNull(result);
    }
}
```

**Step 2: Run tests to verify they fail**

```
dotnet test SystemColors\SystemColors.Tests\SystemColors.Tests.csproj --filter "ClassName=SystemColors.Tests.SpatialGridTests"
```
Expected: All 5 fail with "SpatialGrid does not exist" or similar.

**Step 3: Create `SpatialGrid.cs`**

```csharp
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
            = new Dictionary<(int, int, int), List<(double, double, double, RgbColor)>>();

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
                list = new List<(double, double, double, RgbColor)>();
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
```

**Step 4: Run tests to verify they pass**

```
dotnet test SystemColors\SystemColors.Tests\SystemColors.Tests.csproj --filter "ClassName=SystemColors.Tests.SpatialGridTests"
```
Expected: All 5 pass.

**Step 5: Run all tests to check for regressions**

```
dotnet test SystemColors\SystemColors.Tests\SystemColors.Tests.csproj
```
Expected: All tests pass.

**Step 6: Commit**

```bash
git add SystemColors/SystemColors/ColorConfig.cs SystemColors/SystemColors/colors.json SystemColors/SystemColors/SpatialGrid.cs SystemColors/SystemColors.Tests/DisciplineColorMapperTests.cs
git commit -m "feat: add SpatialGrid for insulation color inheritance"
```

---

### Task 3: Add insulation detection and pass 3 to SystemColorsPlugin

**Files:**
- Modify: `SystemColors\SystemColors\SystemColorsPlugin.cs`

> **Note on BoundingBox API:** `ModelItem.BoundingBox()` returns `BoundingBox3D` (a struct).
> Check `bb.IsEmpty` before using `bb.Min` / `bb.Max`. Each has `.X`, `.Y`, `.Z` doubles.
> If `IsEmpty` does not exist on the type, try checking `bb.Min == null` or wrapping in try/catch.
> Verify against the installed Navisworks API reference at:
> `C:\Program Files\Autodesk\Navisworks Manage 2026\Autodesk.Navisworks.Api.dll`

**Step 1: Add insulation item collection to the tree walk**

In `ApplyColors`, the existing single-pass tree walk builds `allItems` and `systemGroups`. Add a third bucket:

```csharp
var insulationItems = new List<ModelItem>();
```

Inside the `foreach (var item in fileNode.DescendantsAndSelf)` loop, after the existing `systemGroups` logic, add:

```csharp
// Detect insulation: has insulation property but no system match
if (!string.IsNullOrEmpty(config.InsulationPropertyName) && string.IsNullOrEmpty(systemValue))
{
    var insulVal = GetPropertyValue(item, null, config.InsulationPropertyName);
    if (insulVal != null)
        insulationItems.Add(item);
}
```

**Step 2: Add pass 3 after the existing pass 2 block**

Add this after the `foreach (var kvp in systemGroups)` block and before `if (anyColored) colored++;`:

```csharp
// Pass 3: color insulation by spatial proximity to system-colored items
if (insulationItems.Count > 0 && systemGroups.Count > 0)
{
    var grid = new SpatialGrid(config.SpatialGridSize);

    // Build anchors from all system-matched items
    foreach (var kvp in systemGroups)
    {
        var systemRgb = mapper.GetSystemColor(kvp.Key);
        if (systemRgb == null) continue;
        foreach (var sysItem in kvp.Value)
        {
            var bb = sysItem.BoundingBox();
            if (bb.IsEmpty) continue;
            grid.AddAnchor(
                (bb.Min.X + bb.Max.X) / 2.0,
                (bb.Min.Y + bb.Max.Y) / 2.0,
                (bb.Min.Z + bb.Max.Z) / 2.0,
                systemRgb.Value);
        }
    }

    // Apply nearest system color to each insulation item
    foreach (var insulItem in insulationItems)
    {
        var bb = insulItem.BoundingBox();
        if (bb.IsEmpty) continue;
        var nearest = grid.FindNearest(
            (bb.Min.X + bb.Max.X) / 2.0,
            (bb.Min.Y + bb.Max.Y) / 2.0,
            (bb.Min.Z + bb.Max.Z) / 2.0);
        if (nearest == null) continue;
        var single = new ModelItemCollection { insulItem };
        doc.Models.OverridePermanentColor(single, ToNavisColor(nearest.Value));
        anyColored = true;
    }
}
```

**Step 3: Build and verify no compile errors**

```
msbuild SystemColors\SystemColors\SystemColors.csproj /p:Configuration=Debug
```
Expected: Build succeeded, 0 errors.

> If `bb.IsEmpty` doesn't compile, check the `BoundingBox3D` type in the API. It may instead
> need `bb.Equals(BoundingBox3D.Empty)` or a null check — adjust accordingly.

**Step 4: Deploy and test in Navisworks**

Copy the built DLL and `colors.json` to the plugin folder:
```
C:\ProgramData\Autodesk\Navisworks Manage 2026\Plugins\SystemColors\
```

Open a model with MECH ductwork and insulation. Click "Apply System Colors". Verify:
- Duct segments receive their system color (supply/return/exhaust)
- Insulation elements receive the same color as the duct they wrap
- Non-duct/non-insulation elements keep their discipline color

**Step 5: Commit**

```bash
git add SystemColors/SystemColors/SystemColorsPlugin.cs
git commit -m "feat: apply system colors to insulation via spatial grid"
```
