%%
Be concise. Focus changes on small, well-scoped edits. Preserve existing public APIs and project-wide nullable/analysis rules.

This repository is a modular .NET 8 assistant composed of focused projects. The host (`Elara.Host`) composes services (DI), wiring audio, STT, LLM, TTS, and an orchestration pipeline. Use the files and examples below to guide edits and code generation.

Key places to look
- Composition root: `Elara.Host/Program.cs` — DI registrations, channels, and the main pipeline wiring.
- Conversation FSM: `Elara.Pipeline/ConversationStateMachine.cs` — state transitions (Quiescent, Listening, Processing, Speaking), suppression logic, and `PromptReady` event.
- Prompt shaping: `Elara.Context/PromptBuilder.cs` and `Elara.Context/Prompt.cs` — how system prompt + context + user input are assembled for the LLM.
- STT/TTS: `Elara.Speech/*` and `Elara.Audio/*` — audio chunking, Streamer, Transcriber, and platform-guarded TTS implementations (Windows-only `System.Speech` vs `NoOpTextToSpeechService`).
- Language model client: `Elara.Intelligence/OllamaLanguageModelService.cs` — how the Host configures system prompts and model settings.
- Config types: `Elara.Configuration/*` and `Elara.Core/*` — strongly-typed config objects bound from `Elara.Host/appsettings.json`.

What you should know before editing
- Build & test: this is a .NET 8 solution. Preferred commands:
  - Build solution: `dotnet build Elara.sln`
  - Run all tests: `dotnet test Elara.sln`
  - Run the host: `dotnet run --project Elara.Host`
  The `README.md` contains the same quick-start steps and notes about the first-run Whisper model download.

- Warnings-as-errors: projects enable nullable reference types and treat warnings as errors. Avoid introducing new analyzers or .NET 8 warnings without explicit justification.

- Platform guards: TTS uses `OperatingSystem.IsWindows()`; non-Windows builds must compile even if Windows-only APIs exist. Use `[SupportedOSPlatform]` or runtime guards as appropriate.

Architecture & data flow (short)
- Audio captured by `Streamer` (uses `Elara.Audio.IAudioProcessor`) writes `AudioChunk` to a bounded `Channel<AudioChunk>`.
- `Transcriber` reads audio chunks, produces `TranscriptionItem` and writes to a transcription channel.
- `ConversationStateMachine` consumes `TranscriptionItem`s and emits a joined prompt string via `PromptReady` when silence indicates end of user speech.
- `PromptHandlingService` (see `Elara.Host`) takes the prompt, uses `PromptBuilder` + `ILanguageModelService` to get LLM output, and optionally uses `ITextToSpeechService` to speak the response. The FSM is notified to suppress feedback while speaking.

Common patterns and conventions
- Small, focused projects: each subproject owns a narrow responsibility (Core, Audio, Speech, Intelligence, Pipeline, Host).
- Strongly-typed configuration: prefer adding a POCO to `Elara.Configuration` and binding in `Program.cs` rather than reading raw configuration strings.
- Event-driven orchestration: state changes and prompt readiness are signaled via events (`StateChanged`, `PromptReady`) — prefer wiring via small services rather than inlining large handlers.
- Time sources are injectable (`Elara.Core.Time.ITimeProvider`) for testability — use `SystemTimeProvider` in Prod and fakes in unit tests.
- Suppression logic: host maintains a suppression window to avoid processing transcriptions produced while AI is speaking. Respect this when changing playback or audio capture timing.

Examples to copy/adapt
- Add DI in `Program.cs` consistent with existing style. Example pattern:
  services.AddSingleton<ISpeechToTextService>(_ => new SpeechToTextService(modelPath));
- Constructing the LLM service: `OllamaLanguageModelService` is configured with `SystemPrompt` and `OutputFilters` — if adding a provider follow the same SystemPrompt + JSON prompt guidance pattern placed in `Program.cs`.
- Emitting a prompt: `ConversationStateMachine` joins buffered `TranscriptionItem.Text` with spaces and raises `PromptReady` exactly once per Processing transition.

Testing notes
- Unit tests live next to projects as `*.UnitTests` projects. When adding behavior, add tests to the corresponding UnitTests project.
- Prefer injecting `ITimeProvider` and faking it for silence/timer related tests (see `Elara.Pipeline.UnitTests`).

Integrations and external dependencies
- Whisper STT model: first-run download happens in `Elara.Host.Program` and caches under platform-specific cache directories; tests assume model is present only for integration tests.
- Ollama LLM: host expects a local Ollama endpoint configured in `appsettings.json` (`LanguageModel.BaseUrl`). Ensure Ollama is running when testing end-to-end.

What NOT to change without confirmation
- Breaking public API types in `Elara.Core/*` and config POCO shapes. These are used across multiple projects and tests.
- Global logging contracts — logging is centralized via `Elara.Logging.Logger.Configure` in `Program.cs` and other components rely on `ComponentLogger`.

Where to add new code
- Follow existing small-project pattern. Add interfaces and implementations to the relevant project (e.g., a new context provider goes under `Elara.Context` and tests under `Elara.Context.UnitTests`).

If you need more context
- Look at `Elara.Host/Program.cs` for composition, `Elara.Pipeline/ConversationStateMachine.cs` for FSM semantics, and `Elara.Context/PromptBuilder.cs` for how prompts are assembled.

If anything in this file is unclear or missing examples you want, reply and I will iterate on specifics (e.g., show how to add a new `IContextProvider` implementation with tests).

%%