# ErnestAi.Sandbox.Chunking.UnitTests

Minimal test suite for validating the sandbox’s speech-to-text pipeline and state behavior using recorded sample runs.

## What these tests do

- Discover scenarios under `SampleRuns/<scenario>/` that contain:
  - `audio.wav` — the recorded session audio
  - `expected.json` — the expected transcription items and tolerances
- Ensure the Whisper model exists in the cross‑platform cache (`Tools/AppPaths.cs`). If missing, tests will:
  - Copy it from the repo’s `ErnestAi.Sandbox.Chunking/Models/Whisper/` if available
  - Otherwise, download from `expected.json`’s `modelUrl` (when provided)
- Transcribe `audio.wav` with `SpeechToTextService` (local model path only) and assert:
  - Character Error Rate (CER) <= tolerance
  - Word Error Rate (WER) <= tolerance

## Adding a new sample run

1) Create a folder: `SampleRuns/<scenario>/<timestamp>/`
2) Place `audio.wav` and `expected.json`
3) In `expected.json` set:
   - `settings.stt.modelFile` (required)
   - Optional `tolerances.cer` and `tolerances.wer` (defaults exist)
   - Optional `settings.stt.modelUrl` if the model isn’t in your local cache or repo

## Notes

- Tests do not manage or alter service internals; they only verify outcomes on recorded inputs.
- The STT service expects a local model path; downloads are handled by the tests when necessary.
