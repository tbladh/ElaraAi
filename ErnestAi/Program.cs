using System.Net.Http.Json;
using System.Speech.Synthesis;
using System.Text.Json;
using NAudio.Wave;
using Whisper.net;

class Program
{
    static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();

        await WakeWordDetector.ListenForWakeWordAsync(
            wakeWord: "ernest",
            modelFileName: "ggml-tiny.en.bin",
            modelUrl: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin",
            onWakeWordDetected: () =>
            {
                Console.WriteLine("Wake word triggered! Running assistant...");
                RunAssistant();
            },
            token: cts.Token
        );
    }

    static async Task RunAssistant()
    {
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;

        // Ensure Whisper model is present
        string modelPath = Path.Combine(exeDir, "ggml-base.en.bin");
        if (!File.Exists(modelPath))
        {
            Console.WriteLine("Downloading Whisper model...");
            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(
                "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin"
            );
            await File.WriteAllBytesAsync(modelPath, bytes);
        }

        Console.WriteLine("Press ENTER to start recording...");
        Console.ReadLine();

        var audioFilePath = Path.Combine(Path.GetTempPath(), "input.wav");
        RecordAudio(audioFilePath, 5); // record for 5 seconds

        Console.WriteLine("Transcribing...");
        var transcription = await TranscribeAsync(audioFilePath, modelPath);
        Console.WriteLine($"You said: {transcription}");

        Console.WriteLine("Sending to Ollama...");
        var aiResponse = await CallOllamaAsync(transcription);
        Console.WriteLine($"AI: {aiResponse}");

        Speak(aiResponse);
    }

    static void RecordAudio(string filePath, int seconds)
    {
        using var waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 1)
        };
        using var writer = new WaveFileWriter(filePath, waveIn.WaveFormat);

        waveIn.DataAvailable += (s, a) =>
        {
            writer.Write(a.Buffer, 0, a.BytesRecorded);
        };

        waveIn.StartRecording();
        Console.WriteLine("Recording...");
        Thread.Sleep(seconds * 1000);
        waveIn.StopRecording();
        Console.WriteLine("Recording stopped.");
    }

    static async Task<string> TranscribeAsync(string filePath, string modelPath)
    {
        using var whisperFactory = WhisperFactory.FromPath(modelPath);
        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage("en")
            .Build();

        await using var fileStream = File.OpenRead(filePath);
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        string result = "";
        await foreach (var segment in processor.ProcessAsync(memoryStream))
        {
            result += segment.Text;
        }
        return result.Trim();
    }

    static async Task<string> CallOllamaAsync(string prompt)
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri("http://127.0.0.1:11434");

        // 1. Get list of installed models
        var tagsResponse = await client.GetFromJsonAsync<OllamaTagsResponse>("/api/tags");
        if (tagsResponse == null || tagsResponse.models.Length == 0)
            throw new Exception("No models found in Ollama.");

        Console.WriteLine(tagsResponse.ToJson());

        // 2. Try to find deepseek-r1 first, otherwise take the first model
        var modelName = tagsResponse.models
            .Select(m => m.name)
            .FirstOrDefault(n => n.StartsWith("deepseek-7b-ex", StringComparison.OrdinalIgnoreCase))
            ?? tagsResponse.models[0].name;

        Console.WriteLine($"Using model: {modelName}");

        // 3. Call /api/generate
        var request = new
        {
            model = modelName,
            prompt = prompt,
            suffix = "",   // optional, can be removed if unused
            stream = false
        };

        var response = await client.PostAsJsonAsync("/api/generate", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();
        return result?.response ?? "";
    }

    public class OllamaTagsResponse
    {
        public OllamaModel[] models { get; set; }
    }

    public class OllamaModel
    {
        public string name { get; set; }
    }

    public class OllamaGenerateResponse
    {
        public string response { get; set; }
    }


    static void Speak(string text)
    {
        using var synth = new SpeechSynthesizer();
        synth.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet);
        synth.Speak(text);
    }

    
}

public static class JsonExtensions
{
    public static string ToJson(this object obj)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        return JsonSerializer.Serialize(obj, options);
    }
}
