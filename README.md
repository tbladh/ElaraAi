# ErnestAi - Local AI Home Assistant

## Project Vision

ErnestAi is a self-contained, fully local AI personality designed for the home environment. The core philosophy centers around privacy, reliability, and extensibility - creating an AI assistant that operates independently of cloud services while maintaining the flexibility to integrate with external services when desired by the user.

The project embodies a "local-first" approach where all core functionality works offline, ensuring your conversations and data remain private and the system continues to function even without internet connectivity.

## Current Implementation

### Architecture Overview

The current implementation provides a working prototype that demonstrates the core pipeline:

```
Wake Word Detection → Audio Recording → Speech-to-Text → LLM Processing → Text-to-Speech → Audio Output
```

### Key Components

1. **Wake Word Detection** (`WakeWordDetector.cs`)
   - Continuous audio monitoring using NAudio
   - Whisper.net-based transcription for wake word recognition
   - Configurable wake word ("ernest" by default)
   - Automatic model downloading and caching

2. **Speech Processing Pipeline** (`Program.cs`)
   - Audio recording with configurable duration
   - Local Whisper model for speech-to-text conversion
   - Integration with local Ollama LLM service
   - System.Speech for text-to-speech output

3. **Dependencies**
   - **NAudio**: Cross-platform audio capture and playback
   - **Whisper.net**: Local speech recognition
   - **System.Speech**: Text-to-speech synthesis
   - **Ollama Integration**: Local LLM inference

### Current Workflow

1. System continuously listens for the wake word "ernest"
2. Upon detection, prompts user to speak (currently requires manual trigger)
3. Records 5 seconds of audio
4. Transcribes audio using local Whisper model
5. Sends transcription to local Ollama instance
6. Speaks the AI response using system TTS

## Future Architecture Vision

### Target Pipeline

The evolved architecture will support a more natural, streaming interaction model:

```
Continuous Audio Stream → Wake Word Detection → Voice Activity Detection → 
Dynamic STT → Context Management → Multi-Model LLM → Tool Execution → 
Contextual TTS → Audio Output
```

### Planned Architectural Components

#### 1. Audio Processing Layer
- **Streaming Wake Word Detection**: Continuous monitoring without buffering delays
- **Voice Activity Detection (VAD)**: Automatic detection of speech start/end
- **Audio Buffer Management**: Efficient audio buffer management and model caching

#### 2. Speech Recognition Layer
- **Streaming STT**: Real-time transcription as user speaks
- **Multi-Model STT**: Support for various Whisper models and alternatives
- **Language Detection**: Only English is supported
- **Audio Buffer Management**: Efficient audio buffer management and model caching

#### 3. Intelligence Layer
- **Multi-Model Support**: 
  - Local models (Ollama, GGML, ONNX)
  - Cloud services (OpenAI, Anthropic, Google) with user permission
  - Specialized models for different tasks
- **Context Management**: Conversation history and session state
- **Response Generation**: Contextually aware and personality-consistent responses

#### 4. Tool Integration Layer
- **Plugin Architecture**: Extensible tool system
- **Home Automation**: Smart device control in the future
- **Information Services**: Weather, news, calendar integration in the future
- **Productivity Tools**: Task management, reminders, note-taking in the future
- **Security Framework**: Permission-based tool access in the future

#### 5. Output Layer
- **Contextual TTS**: Emotion and context-aware speech synthesis in the future
- **Multi-Modal Output**: Text, audio, and visual responses in the future
- **Streaming Audio**: Real-time audio generation and playback in the future
- **Response Formatting**: Structured output for different contexts in the future

### Dependency Injection Architecture

The future architecture will leverage dependency injection for modularity and testability:

```csharp
// Core Services
IWakeWordDetector
IAudioProcessor
ISpeechToTextService
ILanguageModelService
ITextToSpeechService
IToolExecutor

// Configuration Services
IAudioConfiguration
IModelConfiguration
IPersonalityConfiguration

// Storage Services
IConversationHistory
IUserPreferences
IModelCache
```

## Potential Future Directions

### Planned Path: Modular Plugin Architecture

**Focus**: Maximum extensibility and community contribution

**Key Features**:
- Plugin-based architecture with hot-swappable components
- Standardized interfaces for STT, LLM, TTS, and tool providers
- Plugin marketplace and discovery system

**Project Structure**:
```
ErnestAi.Core/           # Core interfaces and base classes
ErnestAi.Audio/          # Audio processing components
ErnestAi.Speech/         # STT/TTS abstractions
ErnestAi.Intelligence/   # LLM and reasoning components
ErnestAi.Tools/          # Tool execution framework
ErnestAi.Plugins/        # Plugin management system
ErnestAi.Host/           # Main application host
ErnestAi.Configuration/  # Settings and preferences
```

**Benefits**:
- Highly extensible and customizable
- Community-driven development
- Easy to add new models and services
- Clear separation of concerns

**Challenges**:
- Complex architecture requiring careful interface design
- Plugin compatibility and versioning
- Performance overhead from abstraction layers

## Technical Considerations

### Dependency Injection Implementation

The project will adopt Microsoft.Extensions.DependencyInjection for service management:

```csharp
// Service Registration
services.AddSingleton<IAudioConfiguration, AudioConfiguration>();
services.AddSingleton<IWakeWordDetector, WhisperWakeWordDetector>();
services.AddScoped<ISpeechToTextService, WhisperSttService>();
services.AddScoped<ILanguageModelService, OllamaService>();
services.AddTransient<IToolExecutor, PluginToolExecutor>();

// Configuration-based service selection
services.AddSingleton<ILanguageModelService>(provider =>
{
    var config = provider.GetService<IModelConfiguration>();
    return config.PreferredProvider switch
    {
        "ollama" => new OllamaService(config),
        "openai" => new OpenAiService(config),
        _ => new OllamaService(config)
    };
});
```

### Solution Structure Evolution

The current single-project structure will evolve into a multi-project solution:

```
ErnestAi.sln
├── src/
│   ├── ErnestAi.Core/              # Core interfaces and models
│   ├── ErnestAi.Audio/             # Audio processing
│   ├── ErnestAi.Speech/            # STT/TTS services
│   ├── ErnestAi.Intelligence/      # LLM services
│   ├── ErnestAi.Tools/             # Tool execution
│   ├── ErnestAi.Configuration/     # Settings management
│   └── ErnestAi.Host/              # Main application
├── plugins/
│   ├── ErnestAi.Plugins.Weather/   # Weather plugin
│   ├── ErnestAi.Plugins.Calendar/  # Calendar plugin
│   └── ErnestAi.Plugins.SmartHome/ # Smart home plugin
├── tests/
│   ├── ErnestAi.Core.Tests/
│   ├── ErnestAi.Audio.Tests/
│   └── ErnestAi.Integration.Tests/
└── docs/
    ├── architecture.md
    ├── plugin-development.md
    └── deployment.md
```

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

## Getting Started

### Prerequisites

- .NET 8.0 or later
- Ollama installed and running locally
- Audio input/output devices

### Quick Start

1. Clone the repository
2. Ensure Ollama is running with a compatible model
3. Build and run the application:
   ```bash
   dotnet build
   dotnet run --project ErnestAi
   ```
4. Say "ernest" to trigger the wake word detection
5. Follow the prompts to interact with the assistant

### Configuration

The system will support configuration through:
- `appsettings.json` for application settings
- Environment variables for sensitive configuration
- User preferences stored locally
- Plugin-specific configuration files

## Contributing

ErnestAi follows the development philosophy outlined in `INFO.ai`:
- Prioritize simplicity over complexity
- Use stable, proven technologies
- Maintain low dependencies for portability
- Design for AI agent comprehension
- Follow standard C# conventions

## Roadmap

### Phase 1: Foundation (Current)
- [x] Basic wake word detection
- [x] Local STT with Whisper
- [x] Ollama LLM integration
- [x] Basic TTS output

### Phase 2: Streaming and Modularity
- [ ] Streaming audio processing
- [ ] Voice activity detection
- [ ] Dependency injection framework
- [ ] Multi-project solution structure
- [ ] Configuration system

### Phase 3: Extensibility
- [ ] Plugin architecture
- [ ] Multi-model support
- [ ] Tool integration framework
- [ ] Web-based configuration interface

### Phase 4: Intelligence
- [ ] Conversation memory
- [ ] Context management
- [ ] Personality system
- [ ] Learning and adaptation

## License

MIT License

## Acknowledgments

- Whisper.net for local speech recognition
- NAudio for cross-platform audio processing
- Ollama for local LLM inference
- The open-source AI community for inspiration and tools
