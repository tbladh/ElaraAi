# Elara AI

Elara is a modular, local-first voice assistant built on .NET 8. The host listens for a wake word, segments incoming audio, transcribes it with Whisper, calls a local language model (Ollama by default), optionally speaks the reply, and persists the conversation context on disk with optional encryption.

## Highlights

- End-to-end audio -> transcription -> LLM -> (optional) text-to-speech pipeline orchestrated by a conversation state machine.
- Pluggable context stack with a file-backed conversation store (`LastN` today, RAG-ready later).
- Strongly-typed configuration (`AppConfig`) loaded through the standard Microsoft configuration pipeline with environment/override support.
- Local-first tooling: Whisper.cpp model download and caching, Ollama HTTP integration, and session recording for regression playback.
- Logging, announcements, and prompt handling kept in dedicated libraries to maintain a lean host surface.

## Solution Layout

- `Elara.Host/` - console host that composes the pipeline, downloads Whisper models, and wires DI.
- `Elara.Audio/` - audio capture primitives (NAudio-based streamer, segmenter utilities, session recording helpers).
- `Elara.Speech/` - Whisper-based speech-to-text wrapper plus Windows `System.Speech` text-to-speech and a cross-platform no-op fallback.
- `Elara.Intelligence/` - Ollama-backed `ILanguageModelService` with structured prompt support and output filtering.
- `Elara.Pipeline/` - `ConversationStateMachine`, transcription handling, and supporting contracts.
- `Elara.Context/` - shared chat data contracts, prompt builder, file-backed conversation store with AES-256-GCM envelopes, and system prompt provider.
- `Elara.Context.LastN/` - last-N context provider that hydrates history from the conversation store.
- `Elara.Configuration/` - strongly-typed configuration models plus the async loader.
- `Elara.Logging/` - lightweight logging abstractions, file logging, and console colorizer.
- `FluentHosting/` - helper abstractions for lightweight hosting scenarios.
- `Elara.Logging`, `Elara.Audio`, `Elara.Speech`, `Elara.Intelligence`, `Elara.Pipeline`, `Elara.Context`, and peers each have corresponding `*.UnitTests/` projects.
- `Elara.Updater.Dev/` - developer tooling for the (future) updater.
- `build/` - scripts for generating and updating third-party notices.
- `ContextManagement.md` - design notes for the context system (current Last-N plus RAG roadmap).

## Getting Started

### Requirements

- .NET 8 SDK
- Windows for `System.Speech` text-to-speech (other platforms transparently use the no-op TTS service)
- Ollama running locally (default base URL `http://localhost:11434`) with the configured model pulled, for example `ollama pull cogito:8b`

### Clone

```bash
git clone https://github.com/tbladh/ElaraAi.git
```

### Build and Test

```bash
dotnet build Elara.sln
dotnet test Elara.sln
```

### Run the Host

```bash
dotnet run --project Elara.Host
```

- Use `dotnet run --project Elara.Host -- --record[=scenarioName]` to capture audio, transcription, and prompt/response metadata under `SampleRuns/<scenarioName>`.
- Press `Q` or `Esc` to stop the host, or `Ctrl+C`.

> **Model download on first run**
>
> The host ensures the configured Whisper model exists before starting the pipeline. On the first run it will download the model (~1.4 GB by default) and cache it under:
>
> - Windows: `%LOCALAPPDATA%\ElaraAi\Cache\Models\Whisper`
> - macOS: `~/Library/Caches/ElaraAi/Models/Whisper`
> - Linux: `$XDG_CACHE_HOME/ElaraAi/Models/Whisper` or `~/.cache/ElaraAi/Models/Whisper`

Conversation history is stored in `Cache/Conversation` alongside the model cache (or the `Context:StorageRoot` you provide). Records are serialized as per-message envelopes and can be encrypted by supplying a key—see the `Context` settings below.

## Configuration

Runtime configuration lives in `Elara.Host/appsettings.json`. `ConfigLoader` builds the final `AppConfig` using:

1. `appsettings.json`
2. `appsettings.{DOTNET_ENVIRONMENT}.json`
3. `appsettings.Override.json`
4. Environment variables
5. Command-line arguments

Set `DOTNET_ENVIRONMENT` (or `ASPNETCORE_ENVIRONMENT`) to switch environments. All configuration is strongly typed; IntelliSense is available inside the project.

Key sections:

- `Segmenter` - RMS/active-ratio thresholds and timing for the VAD that decides when to start or stop buffering speech.
- `Stt` - Whisper language, local model file name, and the download URL used when bootstrapping.
- `LanguageModel` - Ollama provider details:
  - `BaseUrl` and `ModelName` identify the server or model.
  - `SystemPrompt` accepts placeholders like `{WakeWord}`; the host appends guidance describing the JSON payload sent to the model.
  - `OutputFilters` is an optional list of regex patterns applied to the reply before speaking or logging.
- `TextToSpeech` - enable or disable TTS, voice name, playback rate or pitch, and a silent preamble.
- `Host` - wake word, silence timers (`ProcessingSilenceSeconds`, `EndSilenceSeconds`), channel capacities, ticker cadence, and session recording defaults.
- `Context` - conversation persistence and retrieval:
  - `LastN` controls how many historical messages the Last-N provider returns.
  - `Provider` (currently `last-n`) selects the context strategy.
  - `StorageRoot` overrides the cache location; leave empty to use the platform cache directory.
  - `EncryptionKey`, when non-empty, is hashed (SHA-256) and enables AES-256-GCM envelopes for stored messages.
- `Announcements` - optional wake, prompt, quiescence phrases and startup templates with `{WakeWord}`, `{ModelName}`, `{ModelBaseUrl}`, and `{Voice}` placeholders.
- `ElaraLogging` - log level, file directory or pattern, and console timestamp format.

Refer to `ContextManagement.md` for the broader context orchestration plan (Last-N today, RAG tomorrow).

## Runtime Flow

1. `Program.cs` loads configuration, configures logging, and verifies the Whisper model exists (downloading if needed).
2. Audio frames are captured via `Streamer` (`Elara.Audio`) and sent through a bounded channel.
3. `Transcriber` (`Elara.Speech`) consumes frames, invokes Whisper, and emits `TranscriptionItem` instances.
4. `ConversationStateMachine` (`Elara.Pipeline`) monitors wake-word detection and silence windows to raise `PromptReady`.
5. `PromptHandlingService` (`Elara.Host`) persists the user turn, asks the configured `IContextProvider` for history, renders a structured JSON prompt, calls the Ollama-backed `ILanguageModelService`, stores the assistant turn, and optionally speaks the reply.
6. TTS playback is wrapped in a suppression window so microphone input captured during speech is ignored.

The structured prompt passed to Ollama looks like:

```json
{
  "prompt": {
    "history": [
      { "role": "user", "content": "...", "timestampUtc": "..." },
      { "role": "assistant", "content": "...", "timestampUtc": "..." }
    ],
    "user": { "role": "user", "content": "current request", "timestampUtc": "..." },
    "hints": { }
  }
}
```

The system prompt (from configuration plus built-in guidance) requests concise, TTS-friendly responses.

## Testing and Tooling

- Run all unit tests: `dotnet test Elara.sln`.
- Each module has a matching `*.UnitTests` project; prefer adding tests alongside changes.
- Session recordings (`--record`) save WAV audio, transcription JSON, and tolerances under `SampleRuns/` for manual or automated playback.
- Generated logs live under the directory specified by `ElaraLogging.Directory` (relative to the host binary by default).

## Contributing

- Keep changes scoped to the relevant project; each assembly treats nullable warnings as errors.
- Respect platform guards when referencing Windows-only APIs (`System.Speech`).
- Update `ContextManagement.md` if you evolve the context stack or prompt format.
- Regenerate third-party notices (`build/Update-ThirdParty-Notices.cmd`) after adding dependencies.
- Follow the repository `.editorconfig` and keep line endings as LF; if you see noisy diffs run `git add --renormalize .`.
- Run `dotnet test Elara.sln` (or the targeted project test suite) before opening a pull request to catch regressions early.

## License

See `LICENSE.md` for details.
