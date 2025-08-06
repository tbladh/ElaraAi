# ErnestAi.Intelligence

## Overview
ErnestAi.Intelligence handles all language model interactions and intelligence services for the ErnestAi system. It provides implementations for querying local and potentially cloud-based language models, managing context, and processing natural language interactions.

## Purpose
This project is responsible for:
- Interacting with language models (LLMs)
- Managing conversation context
- Processing natural language queries
- Constructing effective prompts
- Handling model responses

## Key Features
- Integration with local language models via Ollama
- Support for both synchronous and streaming responses
- Context management for coherent conversations
- Prompt engineering and optimization
- Response parsing and processing

## Implementation
The project implements the `ILanguageModelService` interface from ErnestAi.Core, providing concrete implementations for language model interactions. It uses HTTP clients to communicate with local or remote language model APIs.

## Dependencies
- ErnestAi.Core - For core interfaces
- HTTP client libraries - For API communication

## Usage
This library is used by other ErnestAi components that need to interact with language models, such as the main conversation flow and tool execution components.

## Documentation
For more detailed information about the architecture and design decisions, see the [INFO.ai](./INFO.ai) file.
