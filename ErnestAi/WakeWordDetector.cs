using NAudio.Utils;
using NAudio.Wave;
using Whisper.net;

public static class WakeWordDetector
{
    public static async Task ListenForWakeWordAsync(
        string wakeWord,
        string modelFileName,
        string modelUrl,
        Action onWakeWordDetected,
        CancellationToken token)
    {
        string modelPath = await EnsureModelAsync(modelFileName, modelUrl);

        using var whisperFactory = WhisperFactory.FromPath(modelPath);
        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage("en")
            .Build();

        using var waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 1),
            BufferMilliseconds = 1500
        };

        var buffer = new MemoryStream();

        waveIn.DataAvailable += async (s, a) =>
        {
            if (token.IsCancellationRequested)
                return;

            buffer.Write(a.Buffer, 0, a.BytesRecorded);

            if (buffer.Length >= waveIn.WaveFormat.AverageBytesPerSecond)
            {
                buffer.Position = 0;

                // Create a WAV header around raw PCM without disposing the underlying stream
                using var wavStream = new MemoryStream();
                using (var writer = new WaveFileWriter(new IgnoreDisposeStream(wavStream), waveIn.WaveFormat))
                {
                    writer.Write(buffer.ToArray(), 0, (int)buffer.Length);
                    writer.Flush();
                }

                wavStream.Position = 0; // rewind for Whisper.NET

                string transcript = "";
                await foreach (var segment in processor.ProcessAsync(wavStream))
                {
                    Console.WriteLine($"Transcribing segment: {segment.Text}");
                    transcript += segment.Text;
                }

                if (!string.IsNullOrWhiteSpace(transcript) &&
                    transcript.IndexOf(wakeWord, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine($"Wake word '{wakeWord}' detected: {transcript}");
                    onWakeWordDetected?.Invoke();
                }

                buffer.SetLength(0);
            }
        };

        Console.WriteLine($"Listening for wake word: \"{wakeWord}\"...");
        waveIn.StartRecording();

        try
        {
            while (!token.IsCancellationRequested)
                Thread.Sleep(100);
        }
        finally
        {
            waveIn.StopRecording();
        }
    }

    /// <summary>
    /// Ensures the Whisper model is present in the executable folder.
    /// Downloads it if missing.
    /// </summary>
    public static async Task<string> EnsureModelAsync(string fileName, string url)
    {
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string modelPath = Path.Combine(exeDir, fileName);

        if (!File.Exists(modelPath))
        {
            Console.WriteLine($"Downloading Whisper model '{fileName}'...");
            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(modelPath, bytes);
            Console.WriteLine("Model downloaded.");
        }

        return modelPath;
    }
}
