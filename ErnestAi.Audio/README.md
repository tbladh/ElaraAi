# ErnestAi.Audio

## Overview
ErnestAi.Audio handles all audio input and output operations for the ErnestAi system. It provides implementations for recording audio from microphones, playing audio through speakers, and managing audio streams for real-time processing.

## Purpose
This project is responsible for:
- Capturing audio from input devices
- Playing audio through output devices
- Processing audio streams in real-time
- Detecting voice activity
- Managing audio formats and conversions

## Key Features
- High-quality audio recording and playback
- Real-time audio streaming for continuous processing
- Voice activity detection to optimize processing
- Audio format conversion and resampling
- Audio device management and selection

## Implementation
The project implements the `IAudioProcessor` interface from ErnestAi.Core, providing concrete implementations for audio processing functionality. It uses NAudio for Windows audio device access and handling.

## Dependencies
- ErnestAi.Core - For core interfaces
- NAudio - For Windows audio capture and playback

## Usage
This library is used by other ErnestAi components that need to record or play audio, such as the wake word detection and speech recognition components.

## Documentation
For more detailed information about the architecture and design decisions, see the [INFO.ai](./INFO.ai) file.
