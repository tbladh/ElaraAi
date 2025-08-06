# ErnestAi.Core

## Overview
ErnestAi.Core is the foundation of the ErnestAi modular architecture. It contains all the core interfaces that define the contracts between different components of the system, enabling a clean separation of concerns and facilitating dependency injection.

## Purpose
This project serves as the backbone of the ErnestAi system, providing:
- Interface definitions for all major system components
- Shared models and data structures
- Common utilities and extension methods
- Type definitions used across the system

## Key Components

### Audio Processing
- `IAudioProcessor` - Interface for audio recording and playback

### Wake Word Detection
- `IWakeWordDetector` - Interface for wake word detection services

### Speech Services
- `ISpeechToTextService` - Interface for speech recognition
- `ITextToSpeechService` - Interface for speech synthesis

### Intelligence Services
- `ILanguageModelService` - Interface for language model integration

### Tool Execution
- `IToolExecutor` - Interface for executing tools and commands

### Configuration
- `IAudioConfiguration` - Audio settings configuration
- `IModelConfiguration` - AI model configuration
- `IPersonalityConfiguration` - Assistant personality configuration

### Storage
- `IConversationHistory` - Conversation history storage
- `IUserPreferences` - User preferences storage
- `IModelCache` - AI model caching

### Plugin System
- `IPluginManager` - Plugin management
- `IPlugin` - Plugin interface

## Development
This project should remain implementation-free, containing only interfaces, abstract classes, and shared models. All concrete implementations should be placed in their respective projects.

## Dependencies
This project has no external dependencies and should remain as lightweight as possible.

## Documentation
For more detailed information about the architecture and design decisions, see the [INFO.ai](./INFO.ai) file.
