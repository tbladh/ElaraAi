# ErnestAi — Local, Private AI Home Assistant

ErnestAi is a self-contained, fully local AI personality for your home. It prioritizes privacy, simplicity, and extensibility. Core features run completely offline and are modular so you can swap implementations as the project grows.

## Getting Started (Under 20 Minutes)

Follow these steps to run locally with a model on Ollama.

### 1) Install prerequisites

- .NET 8 SDK: https://dotnet.microsoft.com/en-us/download
- Ollama: https://ollama.com
- A microphone and speakers/headphones

After installing Ollama, start the service. On Windows it typically runs as a background service and listens on `http://127.0.0.1:11434`.

### 2) Pull or run a model in Ollama

Open a terminal and pull a model. You can replace the tag to your preference.

```bash
ollama pull llama3:latest
# or simply: ollama run llama3:latest
```

If you prefer another model, adapt the `ModelName` accordingly and ensure it’s available in Ollama (`ollama list`).

### 3) Configure ErnestAi

Edit `ErnestAi.Host/appsettings.json`:

- Set your wake word and speech model URLs under `WakeWord` and `SpeechToText`.
- Under `LanguageModels`, provide one or more candidates. Example:

```json
{
  "LanguageModels": [
    {
      "Name": "Llama3-Ollama",
      "Provider": "ollama",
      "ServiceUrl": "http://127.0.0.1:11434",
      "ModelName": "llama3:latest",
      "SystemPrompt": "You are Ernest, a helpful AI assistant.",
      "Priority": 1
    }
  ]
}
```

Notes:
- You may list multiple models; the app picks the first responsive by ascending `Priority`.
- `Filter` is optional and removes matching text via regex. For thinking models, this cleans non-conversational markup.

### 4) Build and run

From the repo root:

```bash
dotnet build ErnestAi.Modular.sln
dotnet run --project ErnestAi.Host
```

The app validates configuration at startup, connects to the selected provider, prints available models, and begins listening for the wake word.

If you see a message about missing models, pull the model in Ollama, update `appsettings.json` if needed, and run again.

## Current Status (Modular Solution)

Minimal end‑to‑end pipeline implemented across modular projects:

Wake Word → Audio Recording → STT (Whisper) → LLM (Ollama) → TTS → Audio Out

Key notes:
- Configuration is strict and explicit: no code defaults. Missing/invalid config fails fast.
- Language models are configured as a prioritized list. The app selects the first responsive entry on startup.
- Warmup pings are minimal and configurable per provider. Purely local providers only.
- Output filtering (regex) removes non-conversational markup from LLM responses (e.g., DeepSeek `<think>…</think>` tags).

## Architecture Overview

### Target Pipeline

The evolved architecture will support a more natural, streaming interaction model:

```
Continuous Audio Stream → Wake Word Detection → Voice Activity Detection → 
Dynamic STT → Context Management → Multi-Model LLM → Tool Execution → 
Contextual TTS → Audio Output
```

### Solution Structure (Brief)

```
ErnestAi.Modular.sln
├── ErnestAi.Core/           # Core interfaces and contracts
├── ErnestAi.Audio/          # Audio capture/recording
├── ErnestAi.Speech/         # Whisper-based STT and TTS
├── ErnestAi.Intelligence/   # LLM services (Ollama)
├── ErnestAi.Tools/          # Tooling stubs
├── ErnestAi.Plugins/        # Plugin stubs
├── ErnestAi.Configuration/  # Strongly-typed config
└── ErnestAi.Host/           # Composition root (main app)
```

### Composition
The host wires the interfaces in `ErnestAi.Core/` to concrete implementations and selects an LLM from `LanguageModels[]` (by priority and provider availability) on startup.

## Next Steps

- Improve natural conversation experience (latency, pacing, flow)
- Integrate additional providers (e.g., ChatGPT) via the same configuration/DI pattern
- Add basic tool use (invoking local capabilities through a stable interface)

## Technical Notes

- Composition via standard .NET DI; concrete services wired in `ErnestAi.Host`.
- Language model selection is configuration-driven and validated on startup.

### Notes
- Non-streaming responses (regex filters applied to output)
- Optional warmup pings for local providers
- Explicit configuration with validation (no code defaults)

### Performance and Scalability

- **Memory Management**: Efficient audio buffer management and model caching
- **CPU Optimization**: Multi-threading for concurrent audio processing and AI inference
- **Model Optimization**: Support for quantized models and hardware acceleration
- **Caching Strategy**: Intelligent caching of models, responses, and user preferences

### Security and Privacy

- **Local Processing**: All sensitive data processed locally by default
- **Encryption**: Encrypted storage of user preferences and conversation history
- **Permission System**: Granular permissions for tool access and data usage
- **Audit Logging**: Comprehensive logging of system actions and data access

## Getting Started (Under 20 Minutes)

Follow these steps to run locally with a model on Ollama.

### 1) Install prerequisites

- .NET 8 SDK: https://dotnet.microsoft.com/en-us/download
- Ollama: https://ollama.com
- A microphone and speakers/headphones

After installing Ollama, start the service. On Windows it typically runs as a background service and listens on `http://127.0.0.1:11434`.

### 2) Pull or run a model in Ollama

Open a terminal and pull a model. You can replace the tag to your preference.

```bash
ollama pull deepseek-7b-ex:latest
# or simply: ollama run deepseek-7b-ex:latest
```

If you prefer another model, adapt the `ModelName` accordingly and ensure it’s available in Ollama (`ollama list`).

### 3) Configure ErnestAi

Edit `ErnestAi.Host/appsettings.json`:

- Set your wake word and speech model URLs under `WakeWord` and `SpeechToText`.
- Under `LanguageModels`, provide one or more candidates. Example:

```json
{
  "LanguageModels": [
    {
      "Name": "DeepSeek-7B-Ollama",
      "Provider": "ollama",
      "ServiceUrl": "http://127.0.0.1:11434",
      "ModelName": "deepseek-7b-ex:latest",
      "SystemPrompt": "You are Ernest, a helpful AI assistant.",
      "Priority": 1,
      "Filter": ["<think>.*?</think>"]
    }
  ]
}
```

Notes:
- You may list multiple models; the app picks the first responsive by ascending `Priority`.
- `Filter` is optional and removes matching text via regex. For thinking models, this cleans non-conversational markup.

### 4) Build and run

Use your IDE or CLI. From the repo root:

```bash
dotnet build ErnestAi.Modular.sln
dotnet run --project ErnestAi.Host
```

The app validates configuration at startup, connects to the selected provider, prints available models, and begins listening for the wake word.

If you see a message about missing models, pull the model in Ollama, update `appsettings.json` if needed, and run again.

## Contributing

ErnestAi follows the development philosophy outlined in `INFO.ai`:
- Prioritize simplicity over complexity
- Use stable, proven technologies
- Maintain low dependencies for portability
- Design for AI agent comprehension
- Follow standard C# conventions

## Roadmap

### Phase 1: Foundation (Current)
- [x] Modular multi-project solution
- [x] Wake word → record → STT → LLM → TTS pipeline
- [x] Strict config with validation (no defaults)
- [x] Prioritized multi-provider LLM selection (Ollama)
- [x] Minimal warmup orchestrator
- [x] Output filters for LLM responses

### Phase 2: Audio/UX Improvements
- [ ] Voice activity detection
- [ ] Adjustable recording and response flow
- [ ] Better logging and diagnostics

### Phase 3: Extensibility
- [ ] Plugin architecture
- [ ] Tool execution framework
- [ ] Additional LLM providers

### Phase 4: Intelligence
- [ ] Conversation memory and context
- [ ] Personality system
- [ ] Learning and adaptation

## License

MIT License

## Acknowledgments

- Whisper.net for local speech recognition
- NAudio for cross-platform audio processing
- Ollama for local LLM inference
- The open-source AI community for inspiration and tools
