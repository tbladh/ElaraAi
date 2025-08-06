# ErnestAi.Host

## Overview
ErnestAi.Host serves as the main entry point and composition root for the ErnestAi system. It brings together all the modular components, configures dependency injection, and manages the application lifecycle.

## Purpose
This project is responsible for:
- Providing the main application entry point
- Configuring dependency injection
- Composing all modular services
- Managing application lifecycle
- Handling cross-cutting concerns

## Key Features
- Dependency injection setup
- Service composition and initialization
- Application lifecycle management
- Error handling and logging
- Graceful shutdown and resource cleanup

## Implementation
The project references all other ErnestAi projects and composes them into a cohesive system. It uses standard .NET dependency injection patterns to wire up all the components and manage their lifecycles.

## Dependencies
- ErnestAi.Core - For core interfaces
- ErnestAi.Audio - For audio processing
- ErnestAi.Speech - For speech services
- ErnestAi.Intelligence - For language model services
- ErnestAi.Tools - For tool execution
- ErnestAi.Plugins - For plugin management
- ErnestAi.Configuration - For configuration management
- Microsoft.Extensions.DependencyInjection - For dependency injection

## Usage
This is the main executable project that users will run to start the ErnestAi system.

## Documentation
For more detailed information about the architecture and design decisions, see the [INFO.ai](./INFO.ai) file.
