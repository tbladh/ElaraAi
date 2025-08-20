using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ErnestAi.Sandbox.Chunking.Core.Interfaces;

namespace ErnestAi.Sandbox.Chunking.Intelligence
{
    /// <summary>
    /// Implementation of ILanguageModelService using Ollama for language model inference
    /// </summary>
    public class OllamaLanguageModelService : ILanguageModelService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly SemaphoreSlim _generateLock = new(1, 1);

        /// <summary>
        /// Gets or sets the currently selected model
        /// </summary>
        public string CurrentModel { get; set; }

        /// <summary>
        /// Gets or sets the system prompt to use with the model
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// Gets the name of the language model service provider
        /// </summary>
        public string ProviderName => "ollama";

        /// <summary>
        /// Regex patterns (as strings) used to filter/snippet out unwanted text from outputs.
        /// </summary>
        public IList<string> OutputFilters { get; set; } = new List<string>();

        /// <summary>
        /// Creates a new instance of the OllamaLanguageModelService
        /// </summary>
        /// <param name="baseUrl">The base URL of the Ollama API</param>
        public OllamaLanguageModelService(string baseUrl)
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };
        }

        /// <summary>
        /// Gets a response from the language model for the given prompt
        /// </summary>
        public async Task<string> GetResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return await SendGenerateAsync(prompt, SystemPrompt, stream: false, cancellationToken);
        }

        /// <summary>
        /// Sends a minimal prompt without a system prompt. Useful for warmup/ping.
        /// </summary>
        public Task<string> BarePromptAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return SendGenerateAsync(prompt, system: null, stream: false, cancellationToken);
        }

        private Task EnsureModelSelectedAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentModel))
            {
                throw new InvalidOperationException("No model configured. Set LanguageModel:ModelName in appsettings.json.");
            }
            return Task.CompletedTask;
        }

        private async Task<string> SendGenerateAsync(string prompt, string system, bool stream, CancellationToken cancellationToken)
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
            public string Model { get; set; }

            [JsonPropertyName("prompt")]
            public string Prompt { get; set; }

            [JsonPropertyName("system")]
            public string System { get; set; }

            [JsonPropertyName("stream")]
            public bool Stream { get; set; }
        }

        private class OllamaGenerateResponse
        {
            [JsonPropertyName("response")]
            public string Response { get; set; }
        }

        private class OllamaTagsResponse
        {
            [JsonPropertyName("models")]
            public OllamaModel[] Models { get; set; }
        }

        private class OllamaModel
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }
        }

        #endregion
    }
}
