# Search Set Updater — Design

**Date:** 2026-05-11
**Status:** Designed, not yet implemented
**Supersedes** the color-application behavior of the existing plugin.

## Motivation

The current plugin applies colors to model elements directly via `OverridePermanentColor`. That works, but every project already has an Appearance Profiler keyed off named search sets in the Sets panel. Driving the visual output from search sets is more idiomatic in Navisworks: sets are auditable, persist with the NWF, and let the user override styling in Appearance Profiler without touching code.

The new plugin replaces direct coloring with **updating the conditions of existing saved searches**. The pattern-matching logic from `colors.json` (keywords + multi-property lookup) translates to OR'd saved-search conditions. Appearance Profiler then handles all coloring downstream.

## Architecture

```
systems.json  ──┐
                 ├──> UpdateSearchSetsPlugin
Document Sets ──┘            │
                              ├──> for each system with searchSets:
                              │      locate set in doc.SelectionSets
                              │      build new SearchConditionCollection
                              │      replace existing conditions
                              └──> preview dialog → commit
                                                    │
                                                    └──> Appearance Profiler (unchanged)
```

The audit plugin is unchanged in spirit — still scans property values and reports unmatched ones — but is updated to consume the new `PropertyTarget` config shape.

## Config (`systems.json`)

Renamed from `colors.json`. Color, transparency, insulation, and discipline-code sections removed. New fields:

```json
{
  "systemPropertyNames": [
    { "name": "System Type",           "category": "Mechanical",    "operator": "equals" },
    { "name": "System Classification", "category": "Mechanical",    "operator": "equals" },
    { "name": "Fabrication Service",   "category": "Element",       "operator": "contains" },
    { "name": "Service Type",          "category": "Identity Data", "operator": "equals" },
    { "name": "Name",                  "category": null,            "operator": "contains" }
  ],
  "systems": [
    {
      "name": "Supply Air",
      "searchSets": ["23-Mechanical/Mechanical - Supply"],
      "keywords": ["Supply Air", "HVAC-SA", "Discharge Air", "Cool Supply", "Cooling Supply", "SA"]
    }
  ]
}
```

### Field reference

- `systemPropertyNames[]` — one entry per property the plugin manages. `category` may be `null` for root-level properties like the item name. `operator` may be `equals`, `contains`, or `wildcardmatch`.
- `systems[].searchSets[]` — list of `"FolderName/SetName"` paths. Root-level sets use just `"Set Name"`. Multiple paths allowed; same set referenced by two systems is rejected at load time.
- `systems[].keywords[]` — value strings to match against each property.

## Condition generation

For each system entry with a non-empty `searchSets`:

1. Locate the named set in `doc.SelectionSets`. Skip with a warning if missing.
2. Build a new condition collection: outer loop = keywords, inner loop = property targets. One `SearchCondition` per (keyword, property) pair.
3. **Operator selection** per condition:
   - The property target's explicit `operator` wins if set.
   - Otherwise: keyword length ≤ 3 → `equals`; ≥ 4 → `contains`. Short keywords are too prone to embedded false positives under `contains` (no word boundaries in Navisworks search).
4. **Flag bitmask** for OR chaining: first condition `flags = 10` (case-insensitive, string), subsequent `flags = 74` (case-insensitive string, new OR set). Matches the pattern used in the user's existing exported sets.
5. Replace the set's existing conditions wholesale via the `ReplaceWithCopy` pattern (Navisworks saved items are immutable, so build a new `SelectionSet` and swap it into the parent `FolderItem`).

## Code structure changes

**Renamed / repurposed:**
- `colors.json` → `systems.json`
- `ColorConfig.cs` → `SystemsConfig.cs`. Drop `Color`, `Transparency`, `InsulationGroupNames`, `InsulationTransparency`, `Disciplines`. Promote `SystemPropertyNames` to `List<PropertyTarget>`. Add `SearchSets` to each system entry.
- `SystemColorsPlugin.cs` → `UpdateSearchSetsPlugin.cs`. New responsibility: drive the set-update pass.
- Ribbon button "Apply System Colors" → "Update Search Sets". XML plugin id stays for install-folder continuity.

**Deleted:**
- `DisciplineColorMapper.cs`
- `SpatialGrid.cs`
- All color/transparency/insulation tests in `DisciplineColorMapperTests.cs`

**New:**
- `SearchSetUpdater.cs`. Methods:
  - `FindSavedSet(DocumentSelectionSets, string folderSlashName) -> SavedItem?`
  - `BuildConditions(SystemEntry, IList<PropertyTarget>) -> SearchConditionCollection`
  - `UpdateSet(SavedItem, SearchConditionCollection) -> void`
- New tests for `BuildConditions` (pure data → conditions, no Navisworks runtime required).

**Untouched:**
- `AuditPlugin.cs` (minor update to read `PropertyTarget` shape; core logic unchanged).
- Project / assembly name stays `SystemColors` to avoid breaking the install path at `C:\ProgramData\Autodesk\Navisworks Manage 2026\Plugins\SystemColors\`.

## Edge cases & safety

- **Preview-then-commit.** Before mutating any sets, show a dialog summarizing how many sets will change and how many conditions each will gain/lose. User confirms or cancels. Makes the destructive replace visible.
- **Set not found.** Logged and skipped. Listed in the final summary popup.
- **Same set referenced by two systems.** Rejected at config load.
- **Empty `keywords` for a system.** Skipped with a warning.
- **Document not saved.** Final popup reminds the user to Ctrl+S to persist.
- **Idempotent re-run.** Replace-conditions means clicking twice produces the same result as clicking once.
- **Hand-crafted conditions are lost on update.** Acknowledged; this is what the preview dialog mitigates. Custom rules should live in `systems.json` (`searchSets` can leave a set untouched by simply not referencing it).

## Out of scope

- Generating new sets that don't already exist in the document. The plugin updates existing sets only.
- Restructuring the Sets folder hierarchy.
- Re-introducing color application. Appearance Profiler handles styling.
- Project / assembly rename. Possible later; not now.

## Open questions

- The exact Navisworks API call sequence for `ReplaceWithCopy` on a `SelectionSet` inside a `FolderItem` — confirm during implementation.
- Whether the preview dialog should be optional (a `"confirmBeforeWrite"` flag in `systems.json`) so power users can skip it once they trust the tool.
