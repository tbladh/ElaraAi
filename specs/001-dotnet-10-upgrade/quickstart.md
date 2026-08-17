# Quickstart Validation: .NET 10 Runtime Upgrade

**Feature**: `001-dotnet-10-upgrade` | **Date**: 2026-08-17

Runnable validation scenarios that prove the upgrade works end-to-end. This is a
validation/run guide only — implementation steps live in `tasks.md` and the
implementation phase.

## Prerequisites

- A .NET 10 SDK is present on the machine. **Verify, do not install** (FR-010):

  ```powershell
  dotnet --list-sdks   # expect a 10.0.x entry, e.g. 10.0.301
  ```

  If no `10.0.x` SDK is listed, stop and install the .NET 10 SDK manually —
  this feature does not install it.
- Ollama running locally with the configured model pulled (for the end-to-end
  host run only; build and tests do not require it).
- A Whisper model available (first host run downloads/caches it).

## Validation Scenarios

### 1. Build the solution on .NET 10 (FR-001, FR-002, SC-001)

```powershell
dotnet build Elara.sln
```

**Expected**: Build succeeds with **0 errors and 0 warnings** (the
warnings-as-errors policy in `Directory.Build.props` means any new warning
fails the build).

### 2. Run the full test suite (FR-003, SC-002)

```powershell
dotnet test Elara.sln
```

**Expected**: All tests pass; **no failures and no newly introduced skips**
relative to the .NET 8 baseline.

### 3. Confirm every project targets net10.0 (FR-001)

```powershell
Select-String -Path .\**\*.csproj -Pattern '<TargetFramework>' |
  Where-Object { $_.Line -notmatch 'net10\.0' }
```

**Expected**: No output (every project is on `net10.0`). Any line returned is a
project that was missed.

### 4. Verify dependency compatibility (FR-005, SC-004)

```powershell
dotnet list Elara.Host/package
dotnet list Elara.Speech/package
dotnet list Elara.Audio/package
dotnet list Elara.Configuration/package
```

**Expected**: `Microsoft.Extensions.*`, `System.Speech` on the 10.0.x line;
`NAudio` and `Whisper.net` on a .NET 10-compatible version **or** a documented
exception recorded in `research.md` (R3/R4 fallback pins).

### 5. End-to-end host run (FR-004, SC-003)

```powershell
dotnet run --project Elara.Host
```

**Expected**: The host reaches the ready state, detects the wake word, and
completes a full audio → transcription → language-model → (optional) speech
cycle with behavior equivalent to the .NET 8 baseline. Press `Q`/`Esc` or
`Ctrl+C` to stop.

### 6. Conversation store read-back (FR-007, SC-005)

**Setup**: With a .NET 8 build, run one conversation so records are written to
the conversation store. Then apply the .NET 10 upgrade and start the host.

**Expected**: Prior history loads correctly and new turns append with **zero
data loss** and no migration.

## Out of Scope for Validation

- Installing the .NET 10 SDK (developer prerequisite).
- Changing product features or user-facing behavior (this is a behavior-
  preserving upgrade).
