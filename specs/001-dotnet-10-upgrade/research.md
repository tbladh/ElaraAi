# Research: .NET 10 Runtime Upgrade

**Feature**: `001-dotnet-10-upgrade` | **Date**: 2026-08-17

This document resolves the technical unknowns from the plan's Technical Context
and records the dependency-compatibility decisions. All `NEEDS CLARIFICATION`
items from the spec are resolved (the only one — SDK install vs. verify — was
settled during `/speckit-clarify`: verify only, never install).

## Environment Verification

- **Decision**: The .NET 10 SDK is verified present before build/run; it is
  never installed by the feature.
- **Rationale**: FR-010 / SC-006 require a verify-only posture. On the current
  build machine `dotnet --list-sdks` reports `10.0.301` and
  `dotnet --list-runtimes` reports `Microsoft.NETCore.App 10.0.9`, so the
  verify step passes here.
- **Alternatives considered**: Auto-installing the SDK when missing (rejected —
  out of scope, machine-specific, requires elevation/network); failing silently
  (rejected — poor developer experience).

## Dependency Compatibility

### R1 — Microsoft.Extensions.* (Hosting, Configuration, Logging)

- **Decision**: Bump to the 10.0.x line (latest stable `10.0.11`).
- **Rationale**: These are Microsoft's own abstractions, versioned in lockstep
  with the runtime. Moving from 8.0.x / 9.0.8 to 10.0.11 is the canonical
  .NET 10 alignment and is API-compatible for the hosting/configuration
  patterns used in `Elara.Host` and `Elara.Configuration`.
- **Alternatives considered**: Staying on 9.0.8 (works on net10.0 but leaves
  the stack misaligned with the runtime and mixes major versions); 8.0.x
  (rejected — oldest, no benefit).

### R2 — System.Speech

- **Decision**: Bump `9.0.8` → `10.0.11`.
- **Rationale**: `System.Speech` is a Microsoft package that tracks the runtime
  major version. The 10.0.x release is the correct .NET 10 pairing. The
  Windows-only usage stays behind the existing `OperatingSystem.IsWindows()`
  / `[SupportedOSPlatform]` guards and the `NoOpTextToSpeechService` fallback
  (Constitution V — Platform Portability).
- **Alternatives considered**: Keeping 9.0.8 (functional but misaligned); a
  third-party TTS (rejected — violates Local-First and adds a new dependency).

### R3 — NAudio

- **Decision**: Bump `2.2.1` → `3.0.0`, **contingent on a clean build and
  passing audio tests**.
- **Rationale**: 3.0.0 is the latest stable and the only path to a current,
  .NET 10-aligned audio stack. However, this is a **major** version bump, so the
  `Elara.Audio` and `Elara.Host` code that touches NAudio types (wave formats,
  capture/playback, `WaveFormat`, stream handling) MUST be re-verified against
  the 3.x API.
- **Alternatives considered**: Staying on 2.2.1 (it will still build on
  net10.0 and is the lowest-risk option if 3.0.0 introduces breaking API
  changes). **Fallback rule**: if 3.0.0 causes compile errors or test failures
  that are not trivially resolvable, pin NAudio at 2.2.1 and record it as a
  documented exception (FR-005) rather than forcing the major bump.
- **RESOLVED (implementation)**: NAudio 3.0.0 was attempted and **broke the
  build** — `WaveInEvent` (used by `Elara.Audio/AudioProcessor.cs`) is no
  longer resolvable because NAudio 3.x restructured its capture APIs into
  separate packages. Per the fallback rule, NAudio is **pinned at 2.2.1**.
  This is a documented, justified exception under FR-005: 2.2.1 builds and
  runs correctly on `net10.0`, and migrating to the NAudio 3.x capture model
  is a separate, non-trivial feature (out of scope for this runtime upgrade).

### R4 — Whisper.net / Whisper.net.AllRuntimes

- **Decision**: Bump `1.8.1` → `1.9.1`, **contingent on a clean build and
  passing speech tests**.
- **Rationale**: 1.9.1 is the latest stable and keeps the STT stack current on
  .NET 10. The `WhisperFactory` / transcription APIs used by
  `Elara.Speech.SpeechToTextService` MUST be re-verified against 1.9.x.
- **Alternatives considered**: Staying on 1.8.1 (functional on net10.0; the
  safe fallback if 1.9.1 changes the factory or streaming API). **Fallback
  rule**: if 1.9.1 breaks the build or tests, pin at 1.8.1 and record a
  documented exception (FR-005).
- **RESOLVED (implementation)**: Whisper.net 1.9.1 was applied and **compiles
  cleanly** with the existing `WhisperFactory`/transcription usage; the full
  test suite remains green. Whisper.net is therefore **upgraded to 1.9.1**
  (no exception needed).

### R5 — Test stack (xunit, Microsoft.NET.Test.Sdk, coverlet)

- **Decision**: No version change.
- **Rationale**: `xunit` 2.9.3, `Microsoft.NET.Test.Sdk` 17.10.0,
  `xunit.runner.visualstudio` 2.8.1, and `coverlet.collector` 6.0.0 are
  runtime-agnostic and already build/test correctly on .NET 10. Changing them
  adds risk with no benefit to this feature.
- **Alternatives considered**: Bumping to newer xunit v3 (rejected — out of
  scope; would be a separate feature with its own migration).

### R6 — Contracts artifact

- **Decision**: Do not create a `contracts/` directory for this feature.
- **Rationale**: The upgrade changes no public API surface, no CLI schema, and
  no external protocol. It is a runtime/dependency retarget of an existing
  modular solution, so there are no new interface contracts to document.
- **Alternatives considered**: Documenting the Ollama request/response shape
  (rejected — unchanged by this feature and already described in the README).

## Risk Summary

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| NAudio 3.0.0 breaking API changes | Medium | Build + audio tests gate; fallback pin to 2.2.1 with documented exception (R3). |
| Whisper.net 1.9.1 API changes | Low-Medium | Build + speech tests gate; fallback pin to 1.8.1 with documented exception (R4). |
| Warnings-as-errors surfacing new analyzer warnings on net10.0 | Low | Resolve or justify each; `Directory.Build.props` policy unchanged. |
| Conversation store format incompatibility | Low | Format is JSON envelopes (runtime-agnostic); verify read-back of a .NET 8-created store (SC-005). |
| `System.Speech` behavior change on net10.0 | Low | Platform guards + no-op fallback unchanged; covered by existing tests. |
