using System.Text.Json;
using System.Net.Http;
using Elara.Speech;
using Elara.Core.Paths;

namespace Elara.UnitTests
{
    public sealed class SampleRunTests
    {
        private sealed class ExpectedSchema
        {
            public Tolerances tolerances { get; set; } = new();
            public Settings settings { get; set; } = new();
            public List<TranscriptItem> transcripts { get; set; } = new();
        }

        private static async Task EnsureFileAsync(string destinationPath, string url)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            if (File.Exists(destinationPath)) return;

            var tmp = destinationPath + ".tmp";
            if (File.Exists(tmp))
            {
                try { File.Delete(tmp); } catch { /* ignore */ }
            }

            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30)
            };

            try
            {
                await using (var respStream = await http.GetStreamAsync(url))
                await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await respStream.CopyToAsync(fs);
                }

                if (File.Exists(destinationPath))
                {
                    // Another parallel test may have completed download first
                    try { File.Delete(tmp); } catch { /* ignore */ }
                    return;
                }
                File.Move(tmp, destinationPath, overwrite: true);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* ignore */ }
                throw new InvalidOperationException($"Failed to download model from '{url}' to '{destinationPath}'.", ex);
            }
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
                var wav = Path.Combine(scenario, TestConstants.Paths.AudioFileName);
                var json = Path.Combine(scenario, TestConstants.Paths.ExpectedJsonFileName);
                if (File.Exists(wav) && File.Exists(json))
                {
                    yield return new object[] { scenario, wav, json };
                }
            }
        }

        private static string GetSampleRunsPaths()
        {
            var testsDir = new DirectoryInfo(AppContext.BaseDirectory);
            return Path.Combine(testsDir.FullName, TestConstants.Paths.SampleRunsFolder);
        }

        [Fact]
        public void EnsureSampleRunsDiscovered()
        {
            var any = DiscoverRuns().Any();
            Assert.True(any,
                "No SampleRuns found. Place recordings under the test project's SampleRuns/<scenario>/ with audio.wav and expected.json.");
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

            // Model path in shared model cache directory (shared with Host)
            var outModelsDir = ModelPaths.GetWhisperModelsDir();
            var modelFile = expected.settings.stt.modelFile ?? string.Empty;
            var dstModelPath = Path.Combine(outModelsDir, modelFile);
            if (!File.Exists(dstModelPath))
            {
                // Determine URL (prefer explicit, fallback to whisper.cpp main)
                var url = expected.settings.stt.modelUrl;
                if (string.IsNullOrWhiteSpace(url))
                {
                    url = $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{modelFile}";
                }
                await EnsureFileAsync(dstModelPath, url);
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
