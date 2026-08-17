---

description: "Task list for the .NET 10 runtime upgrade"
---

# Tasks: .NET 10 Runtime Upgrade

**Input**: Design documents from `specs/001-dotnet-10-upgrade/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

**Tests**: The feature specification requires the existing test suite to pass on
.NET 10 (FR-003, SC-002). Test tasks below therefore **run and verify the
existing suite** — they do not add new product tests.

**Organization**: Tasks are grouped by user story to enable independent
implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Multi-project .NET solution at repository root; each project has its own
  `.csproj`. Paths below are repository-relative.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the environment precondition and a green baseline before
any change.

- [X] T001 Verify a .NET 10 SDK is present via `dotnet --list-sdks` (expect a `10.0.x` entry); if absent, STOP and report the missing SDK — do NOT install it (FR-010, SC-006) — **verified: 10.0.301 present**
- [X] T002 Capture the pre-upgrade baseline by running `dotnet build Elara.sln` and `dotnet test Elara.sln` on `net8.0` and recording the pass/fail counts as the regression reference (SC-002, SC-003) — **baseline: build 0 warn/0 err; tests 25 passed / 0 failed / 0 skipped**

**Checkpoint**: SDK verified present and a green `net8.0` baseline is recorded.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Retarget every project to `net10.0` and align the safe
Microsoft-first-party dependencies. This MUST complete before any user story is
verified.

**⚠️ CRITICAL**: No user story verification can begin until this phase is complete.

- [X] T003 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Core/Elara.Core.csproj`
- [X] T004 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Audio/Elara.Audio.csproj`
- [X] T005 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Speech/Elara.Speech.csproj`
- [X] T006 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Intelligence/Elara.Intelligence.csproj`
- [X] T007 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Pipeline/Elara.Pipeline.csproj`
- [X] T008 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Context/Elara.Context.csproj`
- [X] T009 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Context.LastN/Elara.Context.LastN.csproj`
- [X] T010 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Configuration/Elara.Configuration.csproj`
- [X] T011 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Logging/Elara.Logging.csproj`
- [X] T012 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Host/Elara.Host.csproj`
- [X] T013 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Updater.Dev/Elara.Updater.Dev.csproj`
- [X] T014 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `FluentHosting/FluentHosting.csproj`
- [X] T015 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Audio.UnitTests/Elara.Audio.UnitTests.csproj`
- [X] T016 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Context.LastN.UnitTests/Elara.Context.LastN.UnitTests.csproj`
- [X] T017 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Context.UnitTests/Elara.Context.UnitTests.csproj`
- [X] T018 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Host.UnitTests/Elara.Host.UnitTests.csproj`
- [X] T019 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Intelligence.UnitTests/Elara.Intelligence.UnitTests.csproj`
- [X] T020 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Pipeline.UnitTests/Elara.Pipeline.UnitTests.csproj`
- [X] T021 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `Elara.Speech.UnitTests/Elara.Speech.UnitTests.csproj`
- [X] T022 [P] Change `<TargetFramework>` from `net8.0` to `net10.0` in `FluentHosting.Tests/FluentHosting.Tests.csproj`
- [X] T023 [P] Bump `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Logging.Console` to `10.0.11` in `Elara.Host/Elara.Host.csproj` (research R1)
- [X] T024 [P] Bump `Microsoft.Extensions.Configuration`, `.Binder`, `.Json`, `.EnvironmentVariables`, `.CommandLine` to the `10.0.x` line in `Elara.Configuration/Elara.Configuration.csproj` (research R1)
- [X] T025 [P] Bump `System.Speech` to `10.0.11` in `Elara.Speech/Elara.Speech.csproj` and `Elara.Host/Elara.Host.csproj` (research R2)
- [X] T026 Confirm no project remains on `net8.0` by scanning all `.csproj` for `<TargetFramework>` and resolving any stragglers (FR-001) — **verified: 20/20 on net10.0**

**Checkpoint**: All 20 projects target `net10.0`; first-party dependencies aligned. User story verification can now begin.

---

## Phase 3: User Story 1 - The solution builds and runs on .NET 10 (Priority: P1) 🎯 MVP

**Goal**: The solution compiles cleanly on .NET 10 under the warnings-as-errors
policy, the full test suite passes, and the host completes an end-to-end
interaction.

**Independent Test**: `dotnet build Elara.sln` succeeds with 0 errors/0 new
warnings, `dotnet test Elara.sln` is green, and `dotnet run --project
Elara.Host` completes a full audio → transcription → LLM → (optional) speech
cycle.

### Verification for User Story 1

- [X] T027 [US1] Build the solution with `dotnet build Elara.sln` and confirm 0 errors and 0 new warnings; resolve or justify any new analyzer warnings surfaced by `net10.0` (FR-002, SC-001) — **PASS: 0 warn/0 err. Fixed two new .NET 10 findings: removed redundant `System.Net.Http` package ref (NU1510) in FluentHosting.Tests; replaced `EndOfStream` loop with `ReadLineAsync()==null` (CA2024) in Elara.Updater.Dev/ProcessUtils.cs**
- [X] T028 [US1] Run the full test suite with `dotnet test Elara.sln` and confirm all tests pass with no failures or newly introduced skips versus the T002 baseline (FR-003, SC-002) — **PASS: 25 passed / 0 failed / 0 skipped (matches net8.0 baseline)**
- [X] T029 [US1] Run the host with `dotnet run --project Elara.Host` and confirm it reaches the ready state and completes one full end-to-end interaction (FR-004, SC-003) — **PASS (startup): host reached ready state on net10.0, spoke TTS announcement (System.Speech 10.0.11), started all pipeline tasks, stopped cleanly. Spoken wake-word interaction requires a human + microphone (manual verification).**

**Checkpoint**: User Story 1 is fully functional and independently verifiable — this is the MVP.

---

## Phase 4: User Story 2 - Existing behavior is preserved (Priority: P2)

**Goal**: Observable behavior (transcription, responses, speech, persisted
history, configuration) is equivalent to the `net8.0` baseline.

**Independent Test**: Side-by-side comparison of a representative conversation
and read-back of an existing conversation store shows behavior equivalent to the
`net8.0` baseline with zero data loss.

### Verification for User Story 2

- [X] T030 [US2] Verify configuration behavior is unchanged by starting the upgraded host and confirming all `appsettings.json` settings are honored exactly as before (FR-006) — **PASS: T029 host run loaded wake word 'Margaret', model, voice 'Microsoft Zira Desktop', and 6s/60s silence timers exactly as configured**
- [X] T031 [US2] Verify conversation-store read-back: load a store written by the `net8.0` build, confirm prior history loads and new turns append with zero data loss and no migration (FR-007, SC-005) — **PASS: net10.0 build read back 5 real records (2025–2026) written by net8.0, appended + read a probe, cleaned up; store count unchanged (22)**
- [X] T032 [US2] Perform a side-by-side behavioral comparison of at least one representative conversation against the T002 baseline and record equivalence (SC-003) — **PASS (automated): identical test results (25/0/0), host reaches ready state, TTS speaks, store reads back. Spoken wake-word side-by-side requires a human + microphone (manual verification).**

**Checkpoint**: User Stories 1 AND 2 are independently verifiable.

---

## Phase 5: User Story 3 - Dependencies and tooling remain compatible (Priority: P3)

**Goal**: Third-party dependencies resolve to .NET 10-compatible versions (or a
documented exception), and third-party notices remain regenerable.

**Independent Test**: `dotnet restore` resolves all dependencies, the build and
tests remain green, and third-party notices regenerate without error.

### Implementation for User Story 3

- [X] T033 [US3] Bump `NAudio` from `2.2.1` to `3.0.0` in `Elara.Audio/Elara.Audio.csproj` and `Elara.Host/Elara.Host.csproj`, then re-verify the NAudio API usage in `Elara.Audio/` and `Elara.Host/` compiles (research R3) — **ATTEMPTED: NAudio 3.0.0 broke the build (`WaveInEvent` no longer resolvable — NAudio 3.x restructured capture APIs)**
- [X] T034 [US3] If T033 introduces breaking API changes that are not trivially resolvable, revert `NAudio` to `2.2.1` and record a documented exception in `specs/001-dotnet-10-upgrade/research.md` (FR-005, research R3 fallback) — **DONE: NAudio pinned at 2.2.1; documented exception recorded in research.md R3**
- [X] T035 [US3] Bump `Whisper.net` and `Whisper.net.AllRuntimes` from `1.8.1` to `1.9.1` in `Elara.Speech/Elara.Speech.csproj` and `Elara.Host/Elara.Host.csproj`, then re-verify the `WhisperFactory`/transcription API usage in `Elara.Speech/SpeechToTextService.cs` compiles (research R4) — **PASS: 1.9.1 compiles cleanly, kept**
- [X] T036 [US3] If T035 introduces breaking API changes that are not trivially resolvable, revert `Whisper.net`/`Whisper.net.AllRuntimes` to `1.8.1` and record a documented exception in `specs/001-dotnet-10-upgrade/research.md` (FR-005, research R4 fallback) — **NOT NEEDED: 1.9.1 is compatible**
- [X] T037 [US3] Re-run `dotnet build Elara.sln` and `dotnet test Elara.sln` after the dependency bumps and confirm they remain green (FR-002, FR-003) — **PASS: build 0 warn/0 err; tests 25 passed / 0 failed / 0 skipped**
- [X] T038 [US3] Regenerate third-party notices via `build/Update-ThirdParty-Notices.cmd` and confirm the process completes and reflects the current dependency set (FR-009) — **PASS: regenerated with 50 packages**

**Checkpoint**: All user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final confirmation that the whole feature meets its success
criteria.

- [X] T039 Run the full `quickstart.md` validation sequence (scenarios 1–6) end to end and confirm every expected outcome holds — **PASS: build green, tests green, all 20 projects net10.0, deps compatible (NAudio 2.2.1 exception + Whisper 1.9.1), host reaches ready state, store read-back verified**
- [X] T040 [P] Confirm `Directory.Build.props` still enforces `TreatWarningsAsErrors=true` and that no project opted out — **PASS: policy intact**
- [X] T041 [P] Update `README.md` requirements section to reference the .NET 10 SDK (verify-only, not install) if it still states .NET 8 — **DONE: updated intro + Requirements to .NET 10**
- [X] T042 Final full `dotnet build Elara.sln` and `dotnet test Elara.sln` to confirm the delivered state is green (SC-001, SC-002) — **PASS: build 0 warn/0 err; tests 25 passed / 0 failed / 0 skipped**

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup (T001 SDK verified, T002 baseline). BLOCKS all user stories.
- **User Stories (Phases 3–5)**: All depend on Foundational completion.
  - US1 (P1) is the MVP and should be completed first.
  - US2 (P2) and US3 (P3) can proceed after US1; US3's dependency bumps may
    require re-running US1's build/test verification (T037).
- **Polish (Phase 6)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) — no dependencies on other stories.
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) — independently testable.
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) — its dependency bumps may invalidate US1 verification, so re-run T027/T028 after T033–T036.

### Within Each User Story

- Foundational retarget before any verification.
- Build (T027) before test (T028) before host run (T029).
- Dependency bumps (T033–T036) before their re-verification (T037).
- Story complete before moving to the next priority.

### Parallel Opportunities

- All retarget tasks T003–T022 are independent file edits and can run in parallel.
- First-party dependency bumps T023–T025 can run in parallel (different files).
- US2 verification tasks T030–T032 can run in parallel once US1 is green.

---

## Implementation Strategy

**MVP first**: Complete Phase 1 → Phase 2 → Phase 3 (US1). At that point the
solution builds, tests, and runs on .NET 10 — a shippable, behavior-verified
increment.

**Incremental delivery**: Add US2 (behavior preservation evidence) then US3
(dependency alignment + notices). US3's major-version bumps (NAudio 3.0.0,
Whisper.net 1.9.1) are explicitly gated on the build/tests staying green, with
documented-exception fallbacks, so a risky bump never blocks delivery.

**Risk containment**: The two contingent major bumps (T033, T035) each carry an
immediate fallback task (T034, T036) that pins the prior version and records a
justified exception, keeping the feature within FR-005.
