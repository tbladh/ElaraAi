# ErnestAi.Plugins

## Overview
ErnestAi.Plugins implements a plugin system that allows for extending ErnestAi's functionality without modifying the core codebase. It enables third-party developers to create plugins that add new capabilities, tools, and integrations to the ErnestAi system.

## Purpose
This project is responsible for:
- Discovering and loading plugins
- Managing plugin lifecycle
- Providing plugin isolation and security
- Exposing plugin-provided tools
- Handling plugin configuration and state

## Key Features
- Dynamic plugin discovery and loading
- Plugin lifecycle management (init, enable, disable)
- Plugin versioning and compatibility checking
- Security boundaries for plugins
- Plugin configuration storage

## Implementation
The project implements the `IPluginManager` and related interfaces from ErnestAi.Core, providing concrete implementations for plugin management functionality. It uses reflection and composition patterns to discover and load plugins dynamically.

## Dependencies
- ErnestAi.Core - For core interfaces
- System.Reflection - For dynamic assembly loading
- System.Composition - For plugin discovery and composition

## Usage
This library is used by the ErnestAi.Host project to discover and load plugins, and by other components that need to access plugin-provided functionality.

## Documentation
For more detailed information about the architecture and design decisions, see the [INFO.ai](./INFO.ai) file.
