# ErnestAi.Speech

## Overview
ErnestAi.Speech handles speech-to-text (STT) and text-to-speech (TTS) functionality for the ErnestAi system. It provides implementations for converting spoken language to text and synthesizing natural-sounding speech from text.

## Purpose
This project is responsible for:
- Converting spoken audio to text (speech recognition)
- Converting text to spoken audio (speech synthesis)
- Managing speech models and voices
- Handling different languages and accents
-- Providing speech processing services used by the Host

## Key Features
- High-quality speech recognition using Whisper models
- Natural-sounding text-to-speech synthesis
- Support for voices available via System.Speech
- Optional console transcription output (configurable)

## Implementation
Implements `ISpeechToTextService` and `ITextToSpeechService` from ErnestAi.Core. Uses Whisper.net for local STT and System.Speech for TTS. Transcription events can be surfaced to the Host and optionally logged to console per configuration.

## Dependencies
- ErnestAi.Core - For core interfaces
- Whisper.net - For local speech recognition
- System.Speech - For text-to-speech synthesis

## Usage
This library is used by other ErnestAi components that need to convert between speech and text, such as the main conversation flow and voice interaction components.

## Documentation
For more detailed information about the architecture and design decisions, see the [INFO.ai](./INFO.ai) file.
