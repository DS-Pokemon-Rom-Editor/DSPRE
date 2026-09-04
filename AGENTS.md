# DSPRE agent instructions

This is the authoritative instruction file for Codex and other coding agents working in this
repository. Read it before making changes. Use [DSPRE-Development.md](DSPRE-Development.md) for the
deeper implementation guide, [BUILDING.md](BUILDING.md) for build and packaging details, and
[Research/ResearchNotes.md](Research/ResearchNotes.md) for the research index.

DSPRE Reloaded is a .NET 8 editor for Nintendo DS Pokemon Diamond, Pearl, Platinum, HeartGold, and
SoulSilver ROM projects. It has a cross-platform ROM core and Avalonia UI, with a legacy Windows-only
WinForms shell retained during the transition.

## Scope and safety

- Keep changes limited to the requested work. Preserve unrelated working-tree changes.
- Inspect `git status` and the relevant diff before editing and again before handing work back.
- Do not change Git configuration, remotes, exclusions, hooks, the index, or history unless the
  operator explicitly requests that exact action. Do not stage, commit, push, fetch, reset, restore,
  stash, clean, merge, rebase, or cherry-pick by default.
- Do not add assistant attribution, generated-by notices, trailers, session links, telemetry, or
  vendor metadata.
- Do not install plugins, hooks, MCP servers, or external integrations without explicit approval.
- Treat ROMs, save states, emulator captures, local paths, private repositories, unpublished
  documentation, and session transcripts as non-public. Never copy their identifying paths,
  filenames, quotations, or personal details into tracked files.
- A conclusion learned only from non-public material must be independently verified from the current
  code, Git history, tests, public documentation, or measurement before it becomes repository
  guidance. Otherwise label it unverified and keep identifying details out of the repository.
- The `.claude/` directory, `CLAUDE.md`, migration material, and any external assistant-memory store
  are historical inputs, not current authority. Do not copy them wholesale or create a permanent
  Codex knowledge store without operator review of its location, privacy rules, loading mechanism,
  and maintenance workflow.

## Evidence standard

Historical notes are leads. The current tree and relevant local Git history win when they disagree.
For claims about an earlier change or design intent, inspect the commit that made the change instead
of relying on a summary.

Use these evidence grades when a distinction matters:

1. **Located**: a declaration, path, opcode, or call site was found.
2. **Statically verified**: producers, consumers, dependencies, and competing paths were traced.
3. **Build verified**: the affected projects compile.
4. **Test verified**: meaningful tests exercised the behavior and assertions ran.
5. **Runtime verified**: the relevant workflow was observed with representative data.

Do not present a lower grade as a higher one. A passing test is evidence only if it reached the code
under test and asserted the intended result.

Before adding a new parser, reader, renderer, service, or helper, search the whole solution for an
existing implementation and its callers. Before fixing a symptom, check whether the behavior should
exist at all. Trace both positive evidence and counter-evidence: alternate branches, version gates,
fallbacks, early returns, stale caches, duplicate readers, and failure paths.

For any claimed complete set, derive or cite the authoritative set, enumerate it, compare it with the
implementation, and report exceptions. A few examples do not prove completeness.

## Current solution layout

The source split is physical. Historical `CoreFiles.props` and `AvaloniaFiles.props` source-sharing
files no longer exist.

| Project | Target | Responsibility | Direct project references |
|---|---|---|---|
| `Ekona/` | `net8.0` | Image primitives and application data paths | none |
| `Images/Images/` | `net8.0` | Nintendo DS image formats | `Ekona` |
| `DSPRE.Core/` | `net8.0` | ROM formats, data model, scripts, databases, and non-UI logic | `Ekona`, `Images` |
| `DSPRE.Avalonia/` | `net8.0` | Cross-platform Avalonia UI and entry point | `DSPRE.Core` |
| `DS_Map/` | `net8.0-windows` | Windows host executable and legacy WinForms editors | all four projects above |
| `DSPRE.Tests/` | `net8.0`; also `net8.0-windows` by default on Windows or when opted in elsewhere | xUnit tests | Core and Avalonia; Windows target also references `DS_Map` |

Place files according to their dependencies:

- ROM models, binary formats, file-system logic, script logic, `RomInfo`, and hg-engine code belong in
  `DSPRE.Core/`.
- Avalonia views, view models, UI-only readers, dialogs, and OpenGL controls belong in
  `DSPRE.Avalonia/`.
- WinForms-only editors and the Windows entry point belong in `DS_Map/`.
- `DSPRE.Core` must not reference Avalonia or WinForms. `DSPRE.Avalonia` must not reference WinForms.
- Avoid adding GDI dependencies to cross-platform paths. Existing compatibility code is not a model
  for new cross-platform work.

Views and view models under `DSPRE.Avalonia/Avalonia/` are organized by menu area: `Shell`,
`Pokemon`, `Trainers`, `Items`, `Text`, `World`, `Graphics`, `Battle`, `Audio`, `Tools`, and
`Controls`. Follow the existing namespace and global-using conventions described in
`DSPRE-Development.md`.

## Build, run, and test

Use .NET 8.

```powershell
dotnet build DS_Map.sln -c Release
dotnet build DSPRE.Avalonia/DSPRE.Avalonia.csproj -c Release-Linux
dotnet run --project DS_Map/DSPRE.csproj -c Debug
dotnet run --project DSPRE.Avalonia/DSPRE.Avalonia.csproj
dotnet test DSPRE.Tests/DSPRE.Tests.csproj -f net8.0
```

The Windows `DSPRE` executable starts the Avalonia shell by default. Set
`DSPRE_WINFORMS_SHELL=1` or pass `--winforms` to select the legacy WinForms shell. The
`DSPRE.Avalonia` executable always starts the pure Avalonia shell. `DSPRE_AVALONIA_SHELL` is not a
current selector.

Pass `--beta` to enable beta-gated editors in Release builds. Debug builds enable them automatically.
The gate and its reasons are defined in `DSPRE.Core/BetaEditors.cs`.

On Windows, omitting `-f net8.0` from the test command also runs the `net8.0-windows` target. Use the
single cross-platform target for routine checks unless the Windows-only surface is relevant. Do not
run multiple full test processes concurrently: ROM fixtures and static application state are shared.

Bundled native helpers live in `Tools/` at the repository root and are copied into the output's
`Tools/` directory. `DSPRE.Core/DSUtils/DSUtils.cs` resolves them from `AppContext.BaseDirectory`, so
callers must not depend on the process working directory.

## Test quality rules

- Add or change tests in the subject folder under `DSPRE.Tests/`.
- Tests that mutate static `RomInfo` state belong to the `rom` collection so they do not run in
  parallel with other ROM-state tests.
- Use `RomFiles.Settled` when enumerating files from the shared unpacked NARC cache.
- A missing optional fixture is a real skip, not a passing early return. Use `SkippableFact` and
  `Skip.If` with a useful reason.
- If a sweep can inspect zero records, assert that its checked count is greater than zero.
- Assertions must consume values produced by the code under test. Source-text searches, reflection
  checks, or constants that merely restate the implementation do not establish runtime behavior.
- When practical, watch a new test fail for the intended reason before accepting its passing result.
- Restore operator-owned project state in `finally` blocks. Tests must not leave ROM projects,
  databases, settings, or generated files modified.
- Test ROM locations are supplied through the ignored `testroms.json`, `DSPRE_TEST_ROMS`,
  `DSPRE_TEST_ROMS_CONFIG`, or `DSPRE_TEST_HEARTGOLD`, `DSPRE_TEST_PLATINUM`, and
  `DSPRE_TEST_DIAMOND`. Access them through `DSPRE.Tests/TestRoms.cs` and never commit
  machine-specific paths.

## Implementation conventions

- Prefer root-cause fixes in the owning layer over UI-only workarounds.
- Check `RomInfo.gameFamily` or `RomInfo.gameVersion` before assuming layouts shared by DPPt and HGSS.
- Put ROM offsets and version-specific layout facts in `RomInfo` or an existing centralized format
  table. Do not scatter magic offsets through readers or views.
- Preserve the established hg-engine parsing model. Never infer variable binary layouts from a single
  fixture when a count, table, or pointer is available.
- When switching ROMs or projects, audit static caches and selection state for stale data.
- Use stream-based parsing and explicit bounds checks for binary formats. Preserve unknown fields and
  round-trip behavior unless the format is proven otherwise.
- Reuse existing readers. If two interpretations disagree, determine which one is authoritative and
  consolidate rather than adding a third.
- Avalonia is the primary UI for new features. Keep view logic in view models or shared services and
  keep code-behind thin.
- Opening an editor must go through the established launcher, placement, registry, and beta-gate
  paths. A direct `Show()` often bypasses required lifecycle behavior. Put long first-open work behind
  the established busy overlay.
- Avalonia XAML resources and compiled bindings can fail outside the immediate edit site. Validate the
  owning project after XAML changes, not just the code-behind file.
- Comments should explain constraints or intent, not narrate the code. Keep user-facing text concise
  and avoid em dashes.

## ROM and script workflows

DSPRE supports the current ds-rom project layout and a legacy ndstool layout. Project detection and
conversion are implemented in `DSPRE.Core/RomInfo.cs` and `DSPRE.Core/DSUtils/DSUtils.cs`; overlay
path selection is centralized in `DSPRE.Core/DSUtils/OverlayUtils.cs`. Do not duplicate those rules.

Script files have binary ROM data and optional plaintext files under `expanded/scripts/`. Loading and
saving, timestamp comparison, caching, database-hash invalidation, and rebuild behavior are owned by
`DSPRE.Core/ROMFiles/ScriptFile.cs`. Changes to this workflow require round-trip tests and must cover
both unchanged and externally edited plaintext.

## Research and reference material

Start at [Research/ResearchNotes.md](Research/ResearchNotes.md). The permanent policy for choosing and
recording reference implementations is [Research/ReferenceEcosystem.md](Research/ReferenceEcosystem.md).
Research notes explain formats and evidence; production code remains the authority for current
behavior.

When using an external implementation, document the exact fact independently confirmed, the public
source or measurement used, and any version or game-family boundary. Do not transplant code or claims
without checking licensing, provenance, and compatibility with DSPRE's current architecture.

## Handoff checklist

Before finishing:

1. Re-read the complete diff, including newly created files.
2. Verify every retained path, command, package version, environment variable, project relationship,
   and architectural statement touched by the change.
3. Run validation proportional to risk and say exactly what ran.
4. Report files changed, behavior retained or replaced, omitted historical material, remaining
   uncertainty, and any decisions still requiring operator approval.
