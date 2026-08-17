<!--
Sync Impact Report
==================
Version change: (none) -> 1.0.0  [initial ratification]
Modified principles: none (initial creation)
Added sections:
  - Core Principles (I-VI)
  - Development Workflow
  - Governance
Removed sections: none
Deferred items: none
-->

# Elara Constitution

## Core Principles

### I. Modular Architecture
Each capability lives in a small, focused project with a single
responsibility (`Elara.Core`, `Elara.Audio`, `Elara.Speech`,
`Elara.Intelligence`, `Elara.Pipeline`, `Elara.Context`,
`Elara.Configuration`, `Elara.Logging`, `Elara.Host`). Projects MUST
depend only on what they need, and the host (`Elara.Host`) is the sole
composition root that wires services via dependency injection. New
functionality MUST be placed in the project that owns that
responsibility rather than added to the host.

### II. Test-First (NON-NEGOTIABLE)
Every project has a colocated `*.UnitTests` project. New behavior MUST
ship with tests in the corresponding test project. Tests MUST be
deterministic and fast: prefer in-memory stubs and fakes over real I/O,
network, or model downloads. Timer- and silence-dependent logic MUST be
tested against an injected `ITimeProvider` fake, never real wall-clock
time.

### III. Local-First
Elara is a local-first assistant. The pipeline MUST run without external
cloud services: speech-to-text uses a locally cached Whisper model, the
language model is a local endpoint (Ollama by default), and conversation
context is persisted on local disk. Any dependency on a remote service
MUST be optional, explicitly configured, and degrade gracefully when
unavailable.

### IV. Strongly-Typed Configuration
Runtime configuration MUST be modeled as strongly-typed POCOs in
`Elara.Configuration` and bound through the standard Microsoft
configuration pipeline (`appsettings.json`, environment, override,
command-line). Reading raw configuration strings ad hoc is prohibited.
New settings MUST be added as typed members with sensible defaults.

### V. Platform Portability
The solution MUST compile on all supported platforms. Windows-only APIs
(e.g. `System.Speech` text-to-speech) MUST be guarded at compile time
(`[SupportedOSPlatform]`) and/or runtime (`OperatingSystem.IsWindows()`)
and MUST have a cross-platform fallback (e.g. `NoOpTextToSpeechService`).
A non-Windows build MUST succeed even when Windows-only code paths exist.

### VI. Simplicity
Prefer the smallest design that satisfies the requirement (YAGNI).
Orchestration MUST be event-driven and composed of small, single-purpose
services rather than large inline handlers. Complexity MUST be justified
in the accompanying specification or plan.

## Development Workflow

- Build: `dotnet build Elara.sln`
- Test: `dotnet test Elara.sln`
- Run: `dotnet run --project Elara.Host`
- Projects enable nullable reference types and treat warnings as errors;
  new code MUST introduce no new warnings or analyzers without explicit
  justification.
- Line endings are LF (see `.editorconfig` / `.gitattributes`); keep the
  working tree renormalized.
- Regenerate third-party notices
  (`build/Update-ThirdParty-Notices.cmd`) after adding dependencies.
- Update `ContextManagement.md` when the context stack or prompt format
  changes.

## Governance

This constitution supersedes all other development practices in this
repository. Where a practice conflicts with a principle here, the
principle wins and the conflict MUST be resolved by amending the
constitution, not by exception.

- **Amendments**: Changes to this document MUST be proposed with a clear
  rationale, applied to `.specify/memory/constitution.md`, and recorded
  in a Sync Impact Report.
- **Versioning**: The version follows semantic versioning.
  - MAJOR: removal or redefinition of a principle (backward-incompatible
    governance change).
  - MINOR: addition of a new principle or section, or materially expanded
    guidance.
  - PATCH: clarifications, wording, or typo fixes with no semantic change.
- **Compliance**: Every specification, plan, and implementation MUST be
  checked against these principles before completion. Use
  `.github/copilot-instructions.md` for runtime development guidance.

**Version**: 1.0.0 | **Ratified**: 2026-08-17 | **Last Amended**: 2026-08-17
