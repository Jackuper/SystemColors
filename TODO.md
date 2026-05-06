# TODO

## Before sharing with coworkers
- [ ] Convert plugin to Autodesk Application Package (`.bundle`) format
  - Custom ribbon tab named **C&T** instead of default "Tool Add-ins 1"
  - New install path: `C:\ProgramData\Autodesk\ApplicationPlugins\SystemColors.bundle\`
  - Author `PackageContents.xml` (verify Navisworks 2026 schema first)
  - Old `Plugins\SystemColors\` folder can be removed after switch

## Testing
- [ ] Verify keyword updates from latest `colors.json` work on a real model
  - Sanitary vs Sanitary Vent ordering
  - HHWS / HHWR not stolen by Domestic Hot Water generic `"HW"` keyword
  - Refrigerant entry (cyan)

## Ideas / nice-to-have
- [ ] Allow per-discipline transparency, not just for insulation
- [ ] Optional: add a "reset all colors" ribbon button
