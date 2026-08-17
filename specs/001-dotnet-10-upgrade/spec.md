# Feature Specification: .NET 10 Runtime Upgrade

**Feature Branch**: `001-dotnet-10-upgrade`

**Created**: 2026-08-17

**Status**: Draft

**Input**: User description: "an upgrade to .NET 10 across the board here."

## Clarifications

### Session 2026-08-17

- Q: Should the spec require installing the .NET 10 SDK when it is missing, or only verify its presence? → A: Verify presence only; the spec MUST NOT declare "install the .NET 10 SDK". Installation is a developer/environment prerequisite, out of scope.

## User Scenarios & Testing *(mandatory)*

<!--
  User stories are prioritized as independently testable journeys.
  Each story delivers a viable slice of value on its own.
-->

### User Story 1 - The solution builds and runs on .NET 10 (Priority: P1)

A developer opens the repository, installs the .NET 10 SDK, and is able to
restore, build, and run the assistant host exactly as before. Every project in
the solution targets the .NET 10 runtime, and the host starts, listens for the
wake word, and completes a full audio → transcription → language-model →
(optional) speech response cycle without modification to how it is launched.

**Why this priority**: This is the core of the request. If the solution does
not build and run on .NET 10, the upgrade has no value. Everything else
depends on this working.

**Independent Test**: Install the .NET 10 SDK, then run the solution build,
the full test suite, and the host. All three succeed and the host behaves
identically to the pre-upgrade baseline.

**Acceptance Scenarios**:

1. **Given** a clean machine with only the .NET 10 SDK installed, **When** the
   solution is built, **Then** every project compiles successfully with no
   errors and no new warnings.
2. **Given** a successful build, **When** the host is started, **Then** it
   reaches the ready state and processes a spoken prompt end to end.
3. **Given** the upgraded solution, **When** the full test suite is run,
   **Then** all tests pass with no failures or skips introduced by the upgrade.

---

### User Story 2 - Existing behavior is preserved (Priority: P2)

A user of the assistant experiences no change in functionality after the
upgrade. Wake-word detection, speech-to-text, language-model responses,
text-to-speech, conversation persistence, and configuration all behave the
same as on the previous runtime. The upgrade is transparent to end users.

**Why this priority**: A runtime upgrade that silently changes or breaks
behavior is a regression. Preserving behavior is what makes the upgrade safe
to ship.

**Independent Test**: Compare observable behavior (transcription quality,
response generation, speech output, persisted conversation history, and
configuration handling) against the pre-upgrade baseline using the same inputs
and environment.

**Acceptance Scenarios**:

1. **Given** the same spoken input, **When** the assistant processes it on
   .NET 10, **Then** the transcription and response are equivalent to the
   .NET 8 baseline.
2. **Given** an existing on-disk conversation store, **When** the upgraded host
   reads it, **Then** prior history is loaded correctly and new turns are
   appended without data loss.
3. **Given** the existing configuration files, **When** the upgraded host
   starts, **Then** all settings are honored exactly as before.

---

### User Story 3 - Dependencies and tooling remain compatible (Priority: P3)

A developer's toolchain continues to work after the upgrade. Third-party
libraries resolve to versions compatible with the .NET 10 runtime, the
build/test tooling runs without modification, and the repository's
third-party notices remain accurate.

**Why this priority**: Compatibility of the surrounding toolchain prevents
follow-on breakage, but it is secondary to the solution itself building and
behaving correctly.

**Independent Test**: Restore packages, run the build and tests, and verify
the third-party notices generation completes without error.

**Acceptance Scenarios**:

1. **Given** the upgraded project files, **When** packages are restored,
   **Then** all dependencies resolve to versions compatible with .NET 10.
2. **Given** a compatible dependency set, **When** third-party notices are
   regenerated, **Then** the process completes successfully and reflects the
   current dependency set.

---

### Edge Cases

- What happens when the .NET 10 SDK is not present on the machine? The
  workflow MUST detect this, report a clear message identifying the missing
  .NET 10 SDK, and stop — it MUST NOT attempt to install it.
- What happens when a third-party dependency has no .NET 10-compatible
  version yet? The upgrade MUST identify such dependencies and either select a
  compatible version or document a justified exception before proceeding.
- How does the system handle a machine that has both the .NET 8 and .NET 10
  SDKs installed? The solution MUST target .NET 10 explicitly and build
  correctly regardless of which SDKs are present.
- What happens if a Windows-only code path (e.g. speech synthesis) behaves
  differently on the new runtime? The platform guards and cross-platform
  fallbacks MUST continue to work, and non-Windows builds MUST still compile.
- How are persisted conversation records created on the older runtime handled?
  They MUST remain readable by the upgraded host (no forced data migration).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST target the .NET 10 runtime in every project of
  the solution.
- **FR-010**: The build/run workflow MUST verify that a .NET 10 SDK is present
  before building or running, and MUST surface a clear, actionable message
  identifying the missing SDK if it is not. The specification MUST NOT declare
  or perform installation of the .NET 10 SDK; installation is a
  developer/environment prerequisite and is out of scope.
- **FR-002**: The system MUST build successfully on the .NET 10 SDK with no
  errors and no new warnings, consistent with the project's
  warnings-as-errors policy.
- **FR-003**: The system MUST pass its full test suite on .NET 10 with no
  failures or newly introduced skips.
- **FR-004**: The assistant host MUST start and complete a full
  audio → transcription → language-model → (optional) speech cycle on .NET 10
  with behavior equivalent to the previous runtime.
- **FR-005**: The system MUST resolve all third-party dependencies to versions
  compatible with the .NET 10 runtime, or document a justified exception for
  any that are not.
- **FR-006**: The system MUST preserve existing configuration behavior, so
  that current configuration files are honored without change.
- **FR-007**: The system MUST preserve the readability of conversation
  records persisted by the previous runtime, without requiring a data
  migration.
- **FR-008**: The system MUST continue to compile and run on all previously
  supported platforms, including the cross-platform fallback for
  Windows-only speech synthesis.
- **FR-009**: The repository's third-party notices MUST remain regenerable and
  accurate after the upgrade.

### Key Entities *(include if feature involves data)*

- **Solution / Projects**: The set of projects that compose the assistant.
  Each MUST declare the .NET 10 target. Key attribute: target runtime version.
- **Dependency Set**: The collection of third-party libraries the solution
  relies on. Key attribute: compatibility with the .NET 10 runtime.
- **Conversation Store**: The on-disk persisted conversation history. Key
  attribute: format compatibility across the runtime upgrade.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of projects in the solution build successfully on the
  .NET 10 SDK with zero errors and zero new warnings.
- **SC-002**: 100% of tests in the suite pass on .NET 10, with no failures or
  newly introduced skips relative to the pre-upgrade baseline.
- **SC-003**: The assistant completes a full end-to-end interaction on .NET 10
  with behavior equivalent to the .NET 8 baseline, verified by a side-by-side
  comparison of at least one representative conversation.
- **SC-004**: 100% of third-party dependencies resolve to .NET 10-compatible
  versions, or each exception is documented with a justification.
- **SC-005**: Conversation records created on the previous runtime are read
  back correctly by the upgraded host with zero data loss.
- **SC-006**: When the .NET 10 SDK is absent, the workflow reports a clear,
  actionable message identifying the missing SDK and does not attempt to
  install it.

## Assumptions

- The .NET 10 SDK is a developer/environment prerequisite. The workflow
  verifies its presence and reports clearly when it is missing, but does not
  install it; installation is out of scope for this feature.
- "Across the board" means every project in the solution is moved to .NET 10
  in a single, coherent change, rather than a partial or mixed-runtime state.
- The upgrade is a runtime/targeting change; it does not by itself alter
  product features, user-facing behavior, or the architecture.
- Third-party dependencies used by the solution have (or will have) .NET 10
  compatible versions; any that do not will be surfaced and resolved before
  completion.
- The existing test suite is a sufficient baseline for detecting behavioral
  regressions introduced by the runtime change.
- No breaking changes to persisted data formats are introduced; existing
  conversation records remain readable.
