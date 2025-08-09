# ErnestAi.Tools

## Overview
ErnestAi.Tools defines the surface for executing local tools/capabilities requested by the assistant.

## Purpose
This project is responsible for:
- Providing a standardized interface for tool execution
- Hosting minimal, local tools in future iterations

## Current Status
- Interface-first. Concrete tools are not yet implemented.
- Next steps focus on adding a small set of local tools behind the common interface.

## Implementation
Implements (or will implement) `IToolExecutor` from ErnestAi.Core. Concrete tools will be added incrementally.

## Dependencies
- ErnestAi.Core — For core interfaces

## Usage
Used by the Host when invoking capabilities via `IToolExecutor`.

## Documentation
For more detailed information about the architecture and design decisions, see the [INFO.ai](./INFO.ai) file.
