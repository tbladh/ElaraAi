# Context Management and Prompt Building

This document specifies a minimal, extensible architecture for conversation context management and system prompt building for Elara. It is designed to work with the current Ollama-based model integration and to evolve into RAG later, without changing higher layers. Changes in this revision:

- Dynamic context sizing ("Last N") is supported at call time, not constructor time.
- `IContextProvider.GetContextAsync(...)` now receives the current prompt and desired `n` to enable both Last-N and RAG/embedding strategies.
- Timestamps are required on every stored record and the prompt carries the current time for LLM awareness.
- Conversation records are symmetrically encrypted on disk with a key read from `appsettings.json` (hashed before use). Default key is `"replace-me-before-deployment"` and must be replaced for production.
- Records are stored under the default cache directory (see `AppPaths.GetCacheRoot()`), in a `Conversation/` subdirectory.

Key constraints:
- No "session" concept in the conversation architecture (sessions are only for unit-test recordings).
- Persist prompt and response separately, as complete JSON entities.
- Always record who said what (role awareness).
- Use regular JSON files written atomically when the whole entity is available.
- Keep it simple and extensible.

Related current code:
- `Elara.Pipeline/ConversationStateMachine.cs`: emits `PromptReady` with the aggregated user utterance and handles conversation modes.
- `Elara.Core/Paths/ModelPaths.cs` and `Elara.Host/Tools/AppPaths.cs`: resolve cache roots consistently.
- `Elara.Core`/`Elara.Host` logging and configuration patterns should be followed.
- `Elara.Intelligence/OllamaLanguageModelService.cs`: current LLM integration using `SystemPrompt` and a single `prompt` string.


## High-Level Architecture

```
ConversationStateMachine (PromptReady: string)
           |
           v
  Conversation Orchestrator (host)
    - Append User message -> IConversationStore
    - Build Prompt        -> IPromptBuilder
    - Render for provider -> IPromptRenderer
    - Call LLM            -> ILanguageModelService
    - Append Assistant message -> IConversationStore
    - (Optionally TTS)
```

- Context acquisition is pluggable via `IContextProvider` (first: Last-N; later: RAG).
- Prompt system text is provided by `ISystemPromptProvider` (config-backed, later dynamic).
- Provider-specific rendering is abstracted via `IPromptRenderer`.


## Data Types

```csharp
namespace Elara.Host.Conversation;

public enum ChatRole
{
    User,
    Assistant,
    System
}

public sealed class ChatMessage
{
    public required ChatRole Role { get; init; }
    public required string Content { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    // Optional metadata for future tagging/redaction/routing
    public IDictionary<string, string>? Metadata { get; init; }
}

public sealed class Prompt
{
    public required string System { get; init; }
    public required IReadOnlyList<ChatMessage> Context { get; init; }
    public required string UserInput { get; init; }
    // Current UTC time, always provided to LLM for temporal awareness
    public required DateTimeOffset NowUtc { get; init; }
    public IDictionary<string, string>? Hints { get; init; }
}
```


## Interfaces

```csharp
namespace Elara.Host.Conversation;

public interface IConversationStore
{
    Task AppendMessageAsync(ChatMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessage>> ReadTailAsync(int n, CancellationToken ct = default);
}

public interface IContextProvider
{
    // Provide the current prompt (may be used for retrieval/embedding) and desired context size.
    Task<IReadOnlyList<ChatMessage>> GetContextAsync(string currentPrompt, int n, CancellationToken ct = default);
}

public interface ISystemPromptProvider
{
    Task<string> GetSystemPromptAsync(CancellationToken ct = default);
}

public interface IPromptBuilder
{
    Task<Prompt> BuildAsync(string userInput, int desiredContextN, CancellationToken ct = default);
}

// An adapter that converts a Prompt to the provider's request shape
public interface IPromptRenderer<TProviderRequest>
{
    Task<TProviderRequest> RenderAsync(Prompt prompt, CancellationToken ct = default);
}
```

Notes:
- `ILanguageModelService` now accepts a structured prompt (system instructions, context, and the current user turn) rather than a plain string. Rendering should populate that contract for the provider-specific service.


## Minimal Implementations (Conceptual)

These examples show intended shapes. Do not implement yet.

### Last-N Context Provider (dynamic)

```csharp
public sealed class LastNContextProvider : IContextProvider
{
    private readonly IConversationStore _store;
    public LastNContextProvider(IConversationStore store) { _store = store; }
    public Task<IReadOnlyList<ChatMessage>> GetContextAsync(string currentPrompt, int n, CancellationToken ct = default)
        => _store.ReadTailAsync(n, ct);
}
```

### System Prompt Provider (Config-backed)

```csharp
public sealed class SystemPromptProvider : ISystemPromptProvider
{
    private readonly string _basePrompt; // from AppConfig.LanguageModel.SystemPrompt
    public SystemPromptProvider(string basePrompt) => _basePrompt = basePrompt ?? string.Empty;
    public Task<string> GetSystemPromptAsync(CancellationToken ct = default) => Task.FromResult(_basePrompt);
}
```

### Prompt Builder

```csharp
public sealed class PromptBuilder : IPromptBuilder
{
    private readonly ISystemPromptProvider _system;
    private readonly IEnumerable<IContextProvider> _contextProviders; // ordered

    public PromptBuilder(ISystemPromptProvider system, IEnumerable<IContextProvider> contextProviders)
    {
        _system = system;
        _contextProviders = contextProviders;
    }

    public async Task<Prompt> BuildAsync(string userInput, int desiredContextN, CancellationToken ct = default)
    {
        var sys = await _system.GetSystemPromptAsync(ct);
        var context = new List<ChatMessage>();
        foreach (var p in _contextProviders)
            context.AddRange(await p.GetContextAsync(userInput, desiredContextN, ct));

        return new Prompt { System = sys, Context = context, UserInput = userInput, NowUtc = DateTimeOffset.UtcNow };
    }
}
```

### Ollama Prompt Renderer (concat strategy)

```csharp
public sealed class OllamaPromptRenderer : IPromptRenderer<string>
{
    public Task<string> RenderAsync(Prompt prompt, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        if (prompt.Context.Count > 0)
        {
            sb.AppendLine("Previous context:");
            foreach (var m in prompt.Context)
                sb.AppendLine($"{m.Role}: {m.Content}");
            sb.AppendLine();
        }
        sb.AppendLine($"User: {prompt.UserInput}");
        sb.Append("Assistant:");
        return Task.FromResult(sb.ToString());
    }
}
```


## Persistence Strategy (Append-only, per-message JSON files, encrypted)

- Root directory: `Path.Combine(AppPaths.GetCacheRoot(), "Conversation")`. Ensure the directory exists.

- File layout:
  - Directory: `Conversation/`
  - Filename: `yyyyMMddTHHmmssfffZ_{seq}_{role}.json`
    - `seq`: zero-padded in-memory counter to disambiguate equal timestamps within the same process run.
    - `role`: `user`, `assistant`, or `system`.

- File content: single JSON object representing a complete `ChatMessage`, then symmetrically encrypted before writing to disk. Envelope format (example):

```json
{
  "alg": "AES-256-GCM",
  "iv": "base64-nonce-12-bytes",
  "ciphertext": "base64-bytes",
  "tag": "base64-auth-tag"
}
```

Example file content:

```json
{
  "role": "User",
  "content": "What's the weather like tomorrow?",
  "timestampUtc": "2025-08-24T16:36:25.123Z",
  "metadata": {
    "source": "voice",
    "lang": "en"
  }
}
```

Encryption key derivation: take the UTF-8 bytes of the configured key string, compute SHA-256, and use the 32-byte result as the AES-GCM key. The configured string may be human-readable; the default is `"replace-me-before-deployment"`.

### FileConversationStore (concept)

```csharp
public sealed class FileConversationStore : IConversationStore
{
    private readonly string _root; // e.g., CacheRoot/Conversation
    private int _seq = 0;

    public FileConversationStore(string root) { Directory.CreateDirectory(root); _root = root; }

    public async Task AppendMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        var ts = message.TimestampUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'");
        var role = message.Role.ToString().ToLowerInvariant();
        var seq = Interlocked.Increment(ref _seq).ToString("D4");
        var path = Path.Combine(_root, $"{ts}_{seq}_{role}.json");
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = false });
        // Encrypt json -> envelopeJson using AES-256-GCM with key derived from SHA-256(configKey)
        await File.WriteAllTextAsync(path, envelopeJson, ct);
    }

    public async Task<IReadOnlyList<ChatMessage>> ReadTailAsync(int n, CancellationToken ct = default)
    {
        var files = Directory.GetFiles(_root, "*.json")
            .OrderByDescending(f => f) // relies on filename sort
            .Take(n)
            .OrderBy(f => f)           // chronological order for return
            .ToArray();

        var list = new List<ChatMessage>(files.Length);
        foreach (var f in files)
        {
            using var s = File.OpenRead(f);
            // Read envelope, decrypt to json, then deserialize ChatMessage
            var msg = await DeserializeDecryptedAsync(s, ct);
            if (msg != null) list.Add(msg);
        }
        return list;
    }
}
```


## Orchestration with ConversationStateMachine

Pseudo-code wiring in the host (no implementation yet):

```csharp
// Somewhere in Program.cs after building DI container
var fsm = new ConversationStateMachine(/* wake word, timers, logger */);

fsm.PromptReady += async (text) =>
{
    var now = DateTimeOffset.UtcNow;
    var userMsg = new ChatMessage { Role = ChatRole.User, Content = text, TimestampUtc = now };
    await store.AppendMessageAsync(userMsg);

    var prompt = await promptBuilder.BuildAsync(text);

    // Render for Ollama
    var structuredPrompt = await renderer.RenderAsync(prompt); // returns StructuredPrompt
    var assistantText = await lm.GetResponseAsync(structuredPrompt);

    var assistantMsg = new ChatMessage { Role = ChatRole.Assistant, Content = assistantText, TimestampUtc = DateTimeOffset.UtcNow };
    await store.AppendMessageAsync(assistantMsg);

    // TTS (if enabled) and FSM transitions happen in host logic
};
```


## Configuration

Add to `appsettings.json`:

```json
{
  "Context": {
    "LastN": 6,
    "StorageRoot": null,
    "EncryptionKey": "replace-me-before-deployment"
  }
}
```

- `StorageRoot = null` means use default cache location via `AppPaths.GetCacheRoot()`; otherwise use explicit path.


## RAG Extensibility (Future)

- Introduce `IRagRetriever` and `IEmbedder` interfaces. Implement `RagContextProvider : IContextProvider` that returns retrieved snippets as `ChatMessage` with `Role = System`.
- `IPromptBuilder` can accept multiple providers; change DI order to enable/disable RAG without altering orchestrator/LLM layers.


## Diagrams

Mermaid (for future renderers that support it):

```mermaid
flowchart TD
    A[ConversationStateMachine\nPromptReady(text)] --> B[Orchestrator]
    B --> C[Append User -> IConversationStore]
    B --> D[IPromptBuilder]
    D --> E[ISystemPromptProvider]
    D --> F[IContextProvider(s)\nLast-N / RAG]
    D --> G[Prompt]
    B --> H[IPromptRenderer]
    H --> I[ILanguageModelService\n(Ollama)]
    I --> J[Append Assistant -> IConversationStore]
```

ASCII (portable):

```
[ConversationStateMachine] --PromptReady--> [Orchestrator]
   |                                            |
   |                                            v
   |                                  [IConversationStore]  (append User)
   |                                            |
   |                                            v
   |                                   [IPromptBuilder]
   |                                      /        \
   |                           [ISystemPrompt]   [IContextProvider(s)]
   |                                            |
   |                                            v
   |                                        [Prompt]
   |                                            |
   |                                            v
   |                                   [IPromptRenderer]
   |                                            |
   |                                            v
   |                                [ILanguageModelService]
   |                                            |
   |                                            v
   |                                  [IConversationStore]  (append Assistant)
```


## Roadmap

- Phase 1 (design): this document.
- Phase 2 (MVP): `FileConversationStore` (encrypted), `LastNContextProvider` (dynamic n), `SystemPromptProvider`, `PromptBuilder` (includes `NowUtc`), `OllamaPromptRenderer`. Host wiring intentionally deferred: do NOT modify `ConversationStateMachine` yet.
- Phase 3 (hardening): tail-read optimization, redaction hook, optional seq persistence.
- Phase 4 (RAG-ready): `IRagRetriever`/`IEmbedder` interfaces and `RagContextProvider` with a stub retriever.
- Phase 5 (providers): add provider-specific renderers that support structured chat arrays where available.


## Notes

- Keep `Announcements` (`Elara.Host/Utilities/Announcements.cs`) separate from conversation persistence unless explicitly wanted; if persisted, use `Role = System` and classify via `Metadata`.
- Unit-test recording sessions (`Elara.Host/Utilities/SessionRecording.cs`) are unrelated to conversation persistence and should not affect this architecture.


---
This file captures the shapes and integration points needed to resume work quickly. Next implementation step is Phase 2 (MVP) as outlined above, without modifying `ILanguageModelService`.
