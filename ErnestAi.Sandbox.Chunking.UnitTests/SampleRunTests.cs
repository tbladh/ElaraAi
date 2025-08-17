using System.Text.Json;
using ErnestAi.Sandbox.Chunking.Speech;
using ErnestAi.Sandbox.Chunking.Tools;

namespace ErnestAi.Sandbox.Chunking.UnitTests
{
    public sealed class SampleRunTests
    {
        private sealed class ExpectedSchema
        {
            public Tolerances tolerances { get; set; } = new();
            public Settings settings { get; set; } = new();
            public List<TranscriptItem> transcripts { get; set; } = new();
        }
        private sealed class Tolerances { public double cer { get; set; } = 0.25; public double wer { get; set; } = 0.4; }
        private sealed class Settings
        {
            public string wakeWord { get; set; } = string.Empty;
            public SegmenterCfg segmenter { get; set; } = new();
            public SttCfg stt { get; set; } = new();
        }
        private sealed class SegmenterCfg { public int SampleRate { get; set; } public int Channels { get; set; } }
        private sealed class SttCfg { public string modelFile { get; set; } = string.Empty; public string modelUrl { get; set; } = string.Empty; }
        private sealed class TranscriptItem { public long Sequence { get; set; } public DateTimeOffset TimestampUtc { get; set; } public string Text { get; set; } = string.Empty; public bool IsMeaningful { get; set; } public int WordCount { get; set; } }

        public static IEnumerable<object[]> DiscoverRuns()
        {
            var sampleRunsDir = GetSampleRunsPaths();
            if (!Directory.Exists(sampleRunsDir)) yield break;
            foreach (var scenario in Directory.EnumerateDirectories(sampleRunsDir))
            {
                var wav = Path.Combine(scenario, "audio.wav");
                var json = Path.Combine(scenario, "expected.json");
                if (File.Exists(wav) && File.Exists(json))
                {
                    yield return new object[] { scenario, wav, json };
                }
            }
        }

        private static string GetSampleRunsPaths()
        {
            var testsDir = new DirectoryInfo(AppContext.BaseDirectory);
            return Path.Combine(testsDir.FullName, "SampleRuns");
        }

        [Fact]
        public void EnsureSampleRunsDiscovered()
        {
            var any = DiscoverRuns().Any();
            Assert.True(any,
                "No SampleRuns found. Place recordings under either: " +
                "(1) <repo>/ErnestAi.Sandbox.Chunking/SampleRuns/<scenario>/<timestamp> with audio.wav and expected.json, " +
                "or (2) <repo>/ErnestAi.Sandbox.Chunking.UnitTests/SampleRuns/... . " +
                "Alternatively set ERNESTAI_REPO_ROOT to the repository root.");
        }

        [Theory]
        [MemberData(nameof(DiscoverRuns))]
        public async Task Transcription_should_match_expected_within_tolerances(string runDir, string wavPath, string expectedJsonPath)
        {
            // Load expected
            var json = await File.ReadAllTextAsync(expectedJsonPath);
            var expected = JsonSerializer.Deserialize<ExpectedSchema>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;

            // Ensure model is available in cross-platform cache directory
            var outModelsDir = AppPaths.GetModelCacheDir();
            var modelFile = expected.settings.stt.modelFile ?? string.Empty;
            var dstModelPath = Path.Combine(outModelsDir, modelFile);

            if (!File.Exists(dstModelPath))
            {
                // Try copy from repo sandbox models
                DirectoryInfo? repoRoot = null;
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                for (int i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
                {
                    if (dir.EnumerateDirectories("ErnestAi.Sandbox.Chunking", SearchOption.TopDirectoryOnly).Any())
                    {
                        repoRoot = dir;
                        break;
                    }
                }
                if (repoRoot != null)
                {
                    var sandboxModelsDir = Path.Combine(repoRoot.FullName, "ErnestAi.Sandbox.Chunking", "Models", "Whisper");
                    var srcModelPath = Path.Combine(sandboxModelsDir, modelFile);
                    if (File.Exists(srcModelPath))
                    {
                        File.Copy(srcModelPath, dstModelPath, overwrite: false);
                    }
                }

                // If still missing and a URL is provided in expected.json, download it (tests are allowed to fetch)
                if (!File.Exists(dstModelPath))
                {
                    var url = expected.settings.stt.modelUrl;
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        using var http = new System.Net.Http.HttpClient();
                        var bytes = await http.GetByteArrayAsync(url);
                        await File.WriteAllBytesAsync(dstModelPath, bytes);
                    }
                }
            }

            // Compose expected text as concatenation of meaningful items
            var expectedText = string.Join(" ", expected.transcripts
                .Where(t => t.IsMeaningful && !string.IsNullOrWhiteSpace(t.Text))
                .OrderBy(t => t.Sequence)
                .Select(t => t.Text.Trim()));

            // Run STT
            var stt = new SpeechToTextService(dstModelPath);
            var actualText = await stt.TranscribeFileAsync(wavPath);

            // Compute metrics
            var cer = TextDistance.CharacterErrorRate(expectedText, actualText);
            var wer = TextDistance.WordErrorRate(expectedText, actualText);

            // Assertions with tolerances from expected.json
            Assert.True(cer <= expected.tolerances.cer, $"CER {cer:F3} exceeded tolerance {expected.tolerances.cer:F3} for run {runDir}\nExpected: {expectedText}\nActual:   {actualText}");
            Assert.True(wer <= expected.tolerances.wer, $"WER {wer:F3} exceeded tolerance {expected.tolerances.wer:F3} for run {runDir}\nExpected: {expectedText}\nActual:   {actualText}");
        }
    }
}
