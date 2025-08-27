using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Elara.Core.Interfaces;
using Elara.Logging;
using Elara.Host.Extensions;

namespace Elara.Host.Intelligence
{
    /// <summary>
    /// Minimal <see cref="ILanguageModelService"/> implementation backed by an Ollama HTTP endpoint.
    /// Provides simple non-streaming generation, model discovery, and basic output post-filtering.
    /// </summary>
    public class OllamaLanguageModelService : ILanguageModelService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly SemaphoreSlim _generateLock = new(1, 1);

        /// <summary>
        /// Gets or sets the currently selected model
        /// </summary>
        public string CurrentModel { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the system prompt to use with the model
        /// </summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Gets the name of the language model service provider
        /// </summary>
        public string ProviderName => "ollama";

        /// <summary>
        /// Regex patterns (as strings) used to filter/snippet out unwanted text from outputs.
        /// Patterns are applied using <see cref="Regex.Replace(string,string,string,RegexOptions)"/> with Singleline.
        /// Invalid patterns are ignored.
        /// </summary>
        public IList<string> OutputFilters { get; set; } = new List<string>();

        /// <summary>
        /// Creates a new instance of the service pointing at an Ollama server base URL.
        /// </summary>
        /// <param name="baseUrl">Base URL of the Ollama API (e.g., http://localhost:11434)</param>
        public OllamaLanguageModelService(string baseUrl)
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };
        }

        /// <summary>
        /// Gets a response from the language model for the given prompt using the configured system prompt.
        /// </summary>
        /// <param name="prompt">User prompt to send to the model.</param>
        /// <param name="cancellationToken">Cancellation token for the HTTP call.</param>
        /// <returns>Post-processed text response (filters applied).</returns>
        public async Task<string> GetResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return await SendGenerateAsync(prompt, SystemPrompt, stream: false, cancellationToken);
        }

        /// <summary>
        /// Sends a minimal prompt without a system prompt. Useful for warmup/ping.
        /// </summary>
        /// <param name="prompt">Prompt text to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Raw model response with filters applied.</returns>
        public Task<string> BarePromptAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return SendGenerateAsync(prompt, system: null, stream: false, cancellationToken);
        }

        /// <summary>
        /// Ensures that <see cref="CurrentModel"/> is set before making an API call.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no model is configured.</exception>
        private Task EnsureModelSelectedAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentModel))
            {
                throw new InvalidOperationException("No model configured. Set LanguageModel:ModelName in appsettings.json.");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Core non-streaming generate path. Serialized by a semaphore to reduce server overload.
        /// </summary>
        /// <param name="prompt">Prompt text.</param>
        /// <param name="system">System prompt, or null to omit.</param>
        /// <param name="stream">Must be false; streaming not supported in this method.</param>
        /// <param name="cancellationToken">Cancellation token passed to HTTP calls.</param>
        /// <returns>Response text with <see cref="OutputFilters"/> applied.</returns>
        /// <exception cref="InvalidOperationException">If called with stream=true.</exception>
        private async Task<string> SendGenerateAsync(string prompt, string? system, bool stream, CancellationToken cancellationToken)
        {
            await EnsureModelSelectedAsync();

            var request = new OllamaGenerateRequest
            {
                Model = CurrentModel,
                Prompt = prompt,
                System = system,
                Stream = stream
            };

            if (!stream)
            {
                await _generateLock.WaitAsync(cancellationToken);
                try
                {
                    // Debug log of full JSON payload being sent to the LLM
                    try
                    {
                        var json = request.ToPrettyJson();
                        Logger.Debug("LLM", $"Sending prompt to LLM:\n{json}");
                    }
                    catch { /* logging best-effort */ }

                    var resp = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
                    resp.EnsureSuccessStatusCode();
                    var result = await resp.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
                    var text = result?.Response ?? string.Empty;
                    return ApplyFilters(text);
                }
                finally
                {
                    _generateLock.Release();
                }
            }

            // Streaming is handled by GetStreamingResponseAsync; this method shouldn't be called with stream=true
            throw new InvalidOperationException("SendGenerateAsync was called with stream=true. Use GetStreamingResponseAsync for streaming responses.");
        }

        /// <summary>
        /// Applies regex-based post-filters to the model output. Invalid patterns are ignored.
        /// </summary>
        /// <param name="text">Input text.</param>
        /// <returns>Filtered text.</returns>
        private string ApplyFilters(string text)
        {
            if (string.IsNullOrEmpty(text) || OutputFilters == null || OutputFilters.Count == 0)
            {
                return text ?? string.Empty;
            }
            var output = text;
            foreach (var pattern in OutputFilters)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;
                try
                {
                    output = Regex.Replace(output, pattern, string.Empty, RegexOptions.Singleline);
                }
                catch
                {
                    // Ignore bad patterns
                }
            }
            return output;
        }

        /// <summary>
        /// Gets the available models for this service
        /// </summary>
        /// <returns>Model names reported by /api/tags, or empty on error.</returns>
        public async Task<IEnumerable<string>> GetAvailableModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<OllamaTagsResponse>("/api/tags");
                if (response?.Models == null || response.Models.Length == 0)
                    return Array.Empty<string>();

                var modelNames = new List<string>();
                foreach (var model in response.Models)
                {
                    if (!string.IsNullOrEmpty(model.Name))
                        modelNames.Add(model.Name);
                }

                return modelNames;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the service is available
        /// </summary>
        /// <returns>True if /api/tags returns success; false on failure/exception.</returns>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        #region Ollama API Models

        private class OllamaGenerateRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = string.Empty;

            [JsonPropertyName("prompt")]
            public string Prompt { get; set; } = string.Empty;

            [JsonPropertyName("system")]
            public string? System { get; set; }

            [JsonPropertyName("stream")]
            public bool Stream { get; set; }
        }

        private class OllamaGenerateResponse
        {
            [JsonPropertyName("response")]
            public string? Response { get; set; }
        }

        private class OllamaTagsResponse
        {
            [JsonPropertyName("models")]
            public OllamaModel[]? Models { get; set; }
        }

        private class OllamaModel
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }

        #endregion
    }
}
