# Data Model: .NET 10 Runtime Upgrade

**Feature**: `001-dotnet-10-upgrade` | **Date**: 2026-08-17

This feature is a runtime/dependency retarget, not a data-feature. The "data"
it touches is (a) the set of projects and their target framework, (b) the
dependency set and version compatibility, and (c) the persisted conversation
store, which must remain readable across the upgrade. No new entities, fields,
or relationships are introduced.

## Entities

### Project (retarget unit)

- **Represents**: A single `.csproj` in the solution.
- **Key attribute**: `TargetFramework` — changes from `net8.0` to `net10.0`.
- **Invariant**: Every project in `Elara.sln` MUST declare `net10.0` after the
  upgrade (FR-001). No project may be left on `net8.0` (no mixed-runtime state).
- **Relationships**: References other projects; the host (`Elara.Host`) is the
  composition root. Reference graph is unchanged.

### Dependency (compatibility unit)

- **Represents**: A third-party `PackageReference` consumed by one or more
  projects.
- **Key attributes**: package id, current version, target version,
  .NET 10 compatibility status.
- **Validation rule**: Each dependency MUST resolve to a .NET 10-compatible
  version, or carry a documented, justified exception (FR-005). See
  `research.md` R1–R5 for the per-package decisions.
- **State transition**: `net8.0-compatible` → `net10.0-compatible` (or
  `exception-documented`).

### Conversation Store (persistence unit)

- **Represents**: The on-disk, file-backed conversation history (per-message
  envelopes, optionally AES-256-GCM encrypted).
- **Key attributes**: storage root, per-message envelope format, optional
  encryption key (SHA-256 derived).
- **Validation rule**: Records written by the .NET 8 build MUST be readable by
  the .NET 10 build with zero data loss and no migration (FR-007, SC-005).
- **Invariant**: The serialization format is JSON and runtime-agnostic; the
  upgrade MUST NOT change the on-disk schema.

## State Transitions

```text
[All projects net8.0]
        |  retarget TargetFramework
        v
[All projects net10.0]  --(build)-->  [Compiles, 0 new warnings]
        |  bump compatible deps
        v
[Dependencies net10.0-compatible or exception-documented]
        |  test
        v
[Full suite green]  --(run)-->  [Host end-to-end behavior equivalent]
```

## Notes

- No identity/uniqueness rules, lifecycle states, or scale assumptions change.
- Data volume is unchanged; the store grows by appended messages exactly as
  before.
