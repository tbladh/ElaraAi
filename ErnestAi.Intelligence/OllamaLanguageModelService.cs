using ErnestAi.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ErnestAi.Intelligence
{
    /// <summary>
    /// Implementation of ILanguageModelService using Ollama for language model inference
    /// </summary>
    public class OllamaLanguageModelService : ILanguageModelService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        /// <summary>
        /// Gets or sets the currently selected model
        /// </summary>
        public string CurrentModel { get; set; }

        /// <summary>
        /// Gets or sets the system prompt to use with the model
        /// </summary>
        public string SystemPrompt { get; set; } = "You are Ernest, a helpful AI assistant.";

        /// <summary>
        /// Gets the name of the language model service provider
        /// </summary>
        public string ProviderName => "Ollama";

        /// <summary>
        /// Creates a new instance of the OllamaLanguageModelService
        /// </summary>
        /// <param name="baseUrl">The base URL of the Ollama API</param>
        public OllamaLanguageModelService(string baseUrl = "http://127.0.0.1:11434")
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

            var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
            return result?.Response ?? string.Empty;
        }

        /// <summary>
        /// Gets a streaming response from the language model for the given prompt
        /// </summary>
        public async IAsyncEnumerable<string> GetStreamingResponseAsync(
            string prompt, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await EnsureModelSelectedAsync();

            // Prepare the request
            var request = new OllamaGenerateRequest
            {
                Model = CurrentModel,
                Prompt = prompt,
                System = SystemPrompt,
                Stream = true
            };

            // Send the request
            var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Read the streaming response
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line))
                    continue;

                string chunkResponse = null;
                try
                {
                    var chunk = JsonSerializer.Deserialize<OllamaGenerateResponse>(line);
                    if (!string.IsNullOrEmpty(chunk?.Response))
                    {
                        chunkResponse = chunk.Response;
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed JSON
                    continue;
                }
                
                if (chunkResponse != null)
                {
                    yield return chunkResponse;
                }
            }
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
