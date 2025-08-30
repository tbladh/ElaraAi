# Elara AI

Elara is a modular, local-first assistant that listens, transcribes, thinks (LLM), and speaks. The solution is split into focused projects to keep concerns separate and enable cross‑platform readiness.

## Quick Start

- Requirements
  - .NET 8 SDK
  - Windows for System.Speech TTS (otherwise a No‑Op TTS implementation is used)

- Clone
  ```bash
  git clone https://github.com/tbladh/ElaraAi.git
  ```

- Build
  ```bash
  dotnet build Elara.sln
  ```

- Run unit tests
  ```bash
  dotnet test Elara.sln
  ```

- Run the host app
  ```bash
  dotnet run --project Elara.Host
  ```

> ⚠️ **Warning (first run download)**
>
> On first run, the host will download the configured Whisper STT model (~1.4 GB) and cache it locally. This is a one‑time download per model file.
> Default cache location:
> - Windows: `%LOCALAPPDATA%/ElaraAi/Cache/Models/Whisper`
> - macOS: `~/Library/Caches/ElaraAi/Models/Whisper`
> - Linux: `$XDG_CACHE_HOME/ElaraAi/Models/Whisper` or `~/.cache/ElaraAi/Models/Whisper`

- Configure the app
  - Edit `Elara.Host/appsettings.json`.
  - Notable sections:
    - `Host`: wake word and timing settings
    - `ElaraLogging`: logging level, directory, file naming, console timestamps
    - `Segmenter`: VAD and audio segmentation thresholds
    - `Stt`: Whisper model file/url and language
    - `LanguageModel`: LLM provider settings (e.g., Ollama base URL and model)
    - `TextToSpeech`: enable/voice/rate/pitch and `PreambleMs` silent preamble

## Repository Structure

- src
  - `Elara.Host/`: Composition root (DI, config, startup)
  - `Elara.Core/`: Shared interfaces, utilities, extensions, configuration types
  - `Elara.Audio/`: Audio processing primitives (e.g., streaming/recording)
  - `Elara.Speech/`: STT and TTS services
    - Windows TTS uses `System.Speech.Synthesis` (guarded by `[SupportedOSPlatform("windows")]`)
    - Cross‑platform `NoOpTextToSpeechService` is used when not on Windows
  - `Elara.Intelligence/`: Language model client/services (e.g., Ollama)
  - `Elara.Pipeline/`: Orchestration (conversation state machine, transcriber)
  - `FluentHosting/`: Minimal hosting and helpers
  - `Elara.Updater.Dev/`: Developer tooling for updater

- tests
  - `Elara.Host.UnitTests/`
  - `Elara.Audio.UnitTests/`
  - `Elara.Speech.UnitTests/`
  - `Elara.Intelligence.UnitTests/`
  - `Elara.Pipeline.UnitTests/`
  - `FluentHosting.Tests/`
  - `Elara.UnitTests/` (legacy/aggregate tests)

## Architecture Overview

- Data flow
  1. Audio input is captured/streamed (`Elara.Audio`)
  2. STT turns audio into text (`Elara.Speech`)
  3. LLM generates a response (`Elara.Intelligence`)
  4. TTS speaks the response (`Elara.Speech`), with an optional silent preamble
  5. `Elara.Pipeline` coordinates the end‑to‑end conversation state

- Configuration binding
  - Strongly‑typed config in `Elara.Core.Configuration` (e.g., `AppConfig`, `TextToSpeechConfig`, `LoggingConfig`)
  - `Elara.Host` binds `appsettings.json` to `AppConfig` and wires DI

- Platform notes
  - TTS: `TextToSpeechService` is Windows‑only; on other OSes, DI picks `NoOpTextToSpeechService`
  - This keeps non‑Windows builds compiling without Windows‑only warnings or runtime failures

## Coding Standards & Warnings

- Nullable reference types are enabled project‑wide
- Treat warnings as errors is enabled; avoid introducing .NET 8 warnings
- Prefer async/await end‑to‑end; avoid sync‑over‑async and `Thread.Sleep` in tests
- Use platform guards for OS‑specific code

## Local LLM (Ollama)

- Ensure Ollama is running locally and the configured model is available
- Configure via `LanguageModel` section in `appsettings.json`

## Logging

- Controlled via the `ElaraLogging` section
- Logs are written to the configured directory with a date‑tokenized filename pattern
- Console timestamps are configurable

## Current Focus Areas

- Stabilize modular boundaries while keeping Host lean (composition only)
- Maintain cross‑platform readiness (particularly for TTS)
- Improve async, cancellation, and streaming ergonomics
- Keep configuration simple and strongly‑typed

## Future Direction

- Add basic context management system (last n conversations, etc).
- Enhance context management system with RAG using local Embedding model via Ollama.
- Cross‑platform TTS backends (e.g., cloud or local engines) with feature parity
- Optional multi‑targeting for OS‑specific features
- Enriched pipeline metrics and diagnostics
- Expand test coverage per module; consolidate legacy `Elara.UnitTests` or rename to integration tests
- Continuous cleanup of nullable annotations and platform guards

## Contributing

- Keep changes small, focused, and aligned with existing patterns (YAGNI)
- Adhere to warnings‑as‑errors; fix analyzer warnings promptly
- Prefer adding tests alongside changes in the corresponding `*.UnitTests` project

## License

See `LICENSE.md` in the repository root.
