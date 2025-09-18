using System;
using System.Collections.Generic;
using Elara.Context;
using Elara.Context.Contracts;
using Elara.Core.Interfaces;
using Elara.Core.Prompts;
using Elara.Logging;
using Elara.Pipeline;

namespace Elara.Host.Utilities
{
    /// <summary>
    /// Encapsulates the PromptReady handling: persists messages, retrieves context, composes the final prompt,
    /// calls the LLM, persists the assistant reply, and drives FSM transitions (and optional TTS).
    /// </summary>
    public sealed class PromptHandlingService
    {
        private readonly ILanguageModelService _llm;
        private readonly ITextToSpeechService _tts;
        private readonly IConversationStore _store;
        private readonly IContextProvider _contextProvider;
        private readonly bool _ttsEnabled;

        public PromptHandlingService(
            ILanguageModelService llm,
            ITextToSpeechService tts,
            IConversationStore store,
            IContextProvider contextProvider,
            bool ttsEnabled)
        {
            _llm = llm;
            _tts = tts;
            _store = store;
            _contextProvider = contextProvider;
            _ttsEnabled = ttsEnabled;
        }

        public void Attach(ConversationStateMachine csm, int lastN, CancellationToken ct)
        {
            csm.PromptReady += (prompt) =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var nowUtc = DateTimeOffset.UtcNow;
                        var context = await _contextProvider.GetContextAsync(prompt, lastN, ct).ConfigureAwait(false)
                                      ?? Array.Empty<ChatMessage>();

                        var contextMessages = ConvertContext(context);

                        var userMsg = new ChatMessage
                        {
                            Role = ChatRole.User,
                            Content = prompt,
                            TimestampUtc = nowUtc,
                            Metadata = null
                        };

                        // Persist the user turn before the LLM call so it is available for subsequent requests
                        await _store.AppendMessageAsync(userMsg, ct).ConfigureAwait(false);

                        var structuredPrompt = new StructuredPrompt
                        {
                            SystemPrompt = _llm.SystemPrompt,
                            Context = contextMessages,
                            User = ToPromptMessage(userMsg)
                        };

                        Logger.Info(HostConstants.Log.Ai, "Calling language model...");
                        var reply = await _llm.GetResponseAsync(structuredPrompt, ct).ConfigureAwait(false);
                        Logger.Info(HostConstants.Log.Ai, $"Response: {reply}");

                        var assistantMsg = new ChatMessage
                        {
                            Role = ChatRole.Assistant,
                            Content = reply,
                            TimestampUtc = DateTimeOffset.UtcNow,
                            Metadata = null
                        };
                        await _store.AppendMessageAsync(assistantMsg, ct).ConfigureAwait(false);

                        if (_ttsEnabled)
                        {
                            csm.BeginSpeaking();
                            try
                            {
                                await _tts.SpeakToDefaultOutputAsync(reply).ConfigureAwait(false);
                            }
                            finally
                            {
                                csm.EndSpeaking();
                            }
                        }
                        else
                        {
                            csm.EndProcessing();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(HostConstants.Log.Ai, $"+Error handling prompt: {ex.Message}");
                        if (csm.IsSpeaking) csm.EndSpeaking(); else csm.EndProcessing();
                    }
                }, ct);
            };
        }

        private static IReadOnlyList<PromptMessage> ConvertContext(IEnumerable<ChatMessage> context)
        {
            var result = new List<PromptMessage>();
            foreach (var message in context)
            {
                result.Add(ToPromptMessage(message));
            }
            return result;
        }

        private static PromptMessage ToPromptMessage(ChatMessage message)
        {
            return new PromptMessage
            {
                Role = RenderRole(message.Role),
                Content = message.Content,
                TimestampUtc = message.TimestampUtc.ToUniversalTime()
            };
        }

        private static string RenderRole(ChatRole role) => role switch
        {
            ChatRole.User => "user",
            ChatRole.Assistant => "assistant",
            ChatRole.System => "system",
            _ => role.ToString().ToLowerInvariant()
        };
    }
}

