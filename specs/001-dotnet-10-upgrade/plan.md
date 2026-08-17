# Implementation Plan: .NET 10 Runtime Upgrade

**Branch**: `001-dotnet-10-upgrade` | **Date**: 2026-08-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-dotnet-10-upgrade/spec.md`

## Summary

Retarget every project in the solution from `net8.0` to `net10.0`, verify the
.NET 10 SDK is present (without installing it), and confirm the solution builds
cleanly under the warnings-as-errors policy, passes its full test suite, and
preserves existing behavior. Third-party dependencies are moved to
.NET 10-compatible versions where a compatible release exists; any that do not
are surfaced with a documented, justified exception.

## Technical Context

**Language/Version**: C# on .NET 10 (target `net10.0`); .NET 10 SDK 10.0.301
verified present on the build machine.

**Primary Dependencies**:
- `NAudio` 2.2.1 → 3.0.0 (major bump; API surface must be re-verified)
- `Microsoft.Extensions.Hosting` / `Microsoft.Extensions.Configuration.*`
  8.0.x / 9.0.8 → 10.0.11
- `System.Speech` 9.0.8 → 10.0.11 (Windows-only, platform-guarded)
- `Whisper.net` / `Whisper.net.AllRuntimes` 1.8.1 → 1.9.1
- Test stack: `Microsoft.NET.Test.Sdk` 17.10.0, `xunit` 2.9.3,
  `xunit.runner.visualstudio` 2.8.1, `coverlet.collector` 6.0.0 (runtime-agnostic;
  no change required)

**Storage**: Local disk — Whisper model cache and the file-backed conversation
store (per-message envelopes, optionally AES-256-GCM). Format must remain
readable across the upgrade (no migration).

**Testing**: xUnit via `dotnet test Elara.sln`; deterministic, in-memory stubs;
`ITimeProvider` fakes for timer/silence logic.

**Target Platform**: Windows (primary; `System.Speech` TTS) with cross-platform
compile support (no-op TTS fallback).

**Project Type**: Modular console host + class libraries (20 projects).

**Performance Goals**: No regression relative to the .NET 8 baseline; the
upgrade is behavior-preserving, not a performance feature.

**Constraints**: Warnings-as-errors (`TreatWarningsAsErrors=true` in
`Directory.Build.props`); nullable reference types enabled; local-first;
platform portability; SDK presence is verified, never installed (FR-010).

**Scale/Scope**: 20 projects (10 libraries/host + 9 test projects +
`FluentHosting`/`FluentHosting.Tests`), all currently `net8.0`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Architecture | PASS | Upgrade is per-project targeting; no new cross-project coupling introduced. |
| II. Test-First (NON-NEGOTIABLE) | PASS | Full suite must pass on net10.0 (FR-003, SC-002); no new behavior without tests. |
| III. Local-First | PASS | No new remote dependencies; Whisper/Ollama remain local. |
| IV. Strongly-Typed Configuration | PASS | Config POCOs unchanged; only the runtime target moves. |
| V. Platform Portability | PASS | `System.Speech` stays platform-guarded; non-Windows build must still compile (FR-008). |
| VI. Simplicity | PASS | Minimal change: retarget + compatible dependency bumps; no new abstractions. |
| Governance: Git Operations | N/A | Commit/push require explicit user permission (handled at commit time). |

**Gate result**: PASS — no violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/001-dotnet-10-upgrade/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
Elara.sln
Directory.Build.props            # TreatWarningsAsErrors (unchanged)
Elara.Core/                      # net8.0 -> net10.0
Elara.Audio/                     # net8.0 -> net10.0  (NAudio)
Elara.Speech/                    # net8.0 -> net10.0  (Whisper.net, System.Speech)
Elara.Intelligence/              # net8.0 -> net10.0
Elara.Pipeline/                  # net8.0 -> net10.0
Elara.Context/                   # net8.0 -> net10.0
Elara.Context.LastN/             # net8.0 -> net10.0
Elara.Configuration/             # net8.0 -> net10.0  (Microsoft.Extensions.Configuration.*)
Elara.Logging/                   # net8.0 -> net10.0
Elara.Host/                      # net8.0 -> net10.0  (Hosting, NAudio, System.Speech, Whisper.net)
Elara.Updater.Dev/               # net8.0 -> net10.0
FluentHosting/                   # net8.0 -> net10.0
*.UnitTests/ + FluentHosting.Tests/   # net8.0 -> net10.0
```

**Structure Decision**: No structural change. This is a runtime/dependency
retarget of the existing modular layout. The only file-level edits are
`<TargetFramework>` in each `.csproj` and `PackageReference` version bumps in
the projects that consume the affected libraries. `contracts/` is intentionally
not created: the upgrade introduces no new external interfaces (see
`research.md`, Decision R5).

## Complexity Tracking

> No Constitution Check violations; this section is not applicable.
