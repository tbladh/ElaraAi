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
- Non‑streaming responses with configurable output filtering (regex)
- Prioritized model selection provided by Host at startup
- Minimal warmup ping support for local providers
- Basic prompt/response handling

## Implementation
Implements `ILanguageModelService` for Ollama. The service:

- Uses non‑streaming generation and applies regex filters to remove non‑conversational markup from outputs.
- Exposes `OutputFilters` (list of regex strings) set from configuration per selected model.
- Returns available models from the provider for informational listing.

## Dependencies
- ErnestAi.Core — Core interfaces
- System.Net.Http — HTTP client for provider APIs

## Usage
This library is used by other ErnestAi components that need to interact with language models, such as the main conversation flow and tool execution components.

## Documentation
For more detailed information about the architecture and design decisions, see the [INFO.ai](./INFO.ai) file.
