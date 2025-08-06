# ErnestAi.Speech

## Overview
ErnestAi.Speech handles speech-to-text (STT) and text-to-speech (TTS) functionality for the ErnestAi system. It provides implementations for converting spoken language to text and synthesizing natural-sounding speech from text.

## Purpose
This project is responsible for:
- Converting spoken audio to text (speech recognition)
- Converting text to spoken audio (speech synthesis)
- Managing speech models and voices
- Handling different languages and accents
- Providing both file-based and streaming speech processing

## Key Features
- High-quality speech recognition using Whisper models
- Natural-sounding text-to-speech synthesis
- Support for multiple languages and voices
- Real-time streaming transcription
- Speech enhancement and noise reduction

## Implementation
The project implements the `ISpeechToTextService` and `ITextToSpeechService` interfaces from ErnestAi.Core, providing concrete implementations for speech processing functionality. It uses Whisper.net for speech recognition and System.Speech for text-to-speech synthesis.

## Dependencies
- ErnestAi.Core - For core interfaces
- Whisper.net - For local speech recognition
- System.Speech - For text-to-speech synthesis

## Usage
This library is used by other ErnestAi components that need to convert between speech and text, such as the main conversation flow and voice interaction components.

## Documentation
For more detailed information about the architecture and design decisions, see the [INFO.ai](./INFO.ai) file.
