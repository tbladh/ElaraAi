# Context Management and Prompt Building

This document specifies a minimal, extensible architecture for conversation context management and system prompt building for Elara. It is designed to work with the current Ollama-based model integration and to evolve into RAG later, without changing higher layers.

Key constraints:
- No "session" concept in the conversation architecture (sessions are only for unit-test recordings).
- Persist prompt and response separately, as complete JSON entities.
- Always record who said what (role awareness).
- Use regular JSON files written atomically when the whole entity is available.
- Keep it simple and extensible.

Related current code:
- `Elara.Host/Pipeline/ConversationStateMachine.cs`: emits `PromptReady` with the aggregated user utterance and handles conversation modes.
- `Elara.Host/Core/Interfaces/ILanguageModelService.cs` and `Elara.Host/Intelligence/OllamaLanguageModelService.cs`: current LLM integration using `SystemPrompt` and a single `prompt` string.


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
    Task<IReadOnlyList<ChatMessage>> GetContextAsync(CancellationToken ct = default);
}

public interface ISystemPromptProvider
{
    Task<string> GetSystemPromptAsync(CancellationToken ct = default);
}

public interface IPromptBuilder
{
    Task<Prompt> BuildAsync(string userInput, CancellationToken ct = default);
}

// An adapter that converts a Prompt to the provider's request shape
public interface IPromptRenderer<TProviderRequest>
{
    Task<TProviderRequest> RenderAsync(Prompt prompt, CancellationToken ct = default);
}
```

Notes:
- We keep `ILanguageModelService` unchanged. For Ollama, `TProviderRequest` will be a pair of strings: `(system, prompt)`, or we can render directly to a string prompt while setting `SystemPrompt` separately.


## Minimal Implementations (Conceptual)

These examples show intended shapes. Do not implement yet.

### Last-N Context Provider

```csharp
public sealed class LastNContextProvider : IContextProvider
{
    private readonly IConversationStore _store;
    private readonly int _lastN;
    public LastNContextProvider(IConversationStore store, int lastN) { _store = store; _lastN = lastN; }
    public Task<IReadOnlyList<ChatMessage>> GetContextAsync(CancellationToken ct = default)
        => _store.ReadTailAsync(_lastN, ct);
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

    public async Task<Prompt> BuildAsync(string userInput, CancellationToken ct = default)
    {
        var sys = await _system.GetSystemPromptAsync(ct);
        var context = new List<ChatMessage>();
        foreach (var p in _contextProviders)
            context.AddRange(await p.GetContextAsync(ct));

        return new Prompt { System = sys, Context = context, UserInput = userInput };
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


## Persistence Strategy (Append-only, per-message JSON files)

- Cross-platform root directory (resolved via a small `IPathProvider`):
  - Windows: `%APPDATA%/Elara/Conversation`
  - macOS: `~/Library/Application Support/Elara/Conversation`
  - Linux: `$XDG_DATA_HOME/Elara/Conversation` or `~/.local/share/Elara/Conversation`

- File layout:
  - Directory: `Conversation/`
  - Filename: `yyyyMMddTHHmmssfffZ_{seq}_{role}.json`
    - `seq`: zero-padded in-memory counter to disambiguate equal timestamps within the same process run.
    - `role`: `user`, `assistant`, or `system`.

- File content: single JSON object representing a complete `ChatMessage`.

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

### FileConversationStore (concept)

```csharp
public sealed class FileConversationStore : IConversationStore
{
    private readonly string _root; // e.g., %APPDATA%/Elara/Conversation
    private int _seq = 0;

    public FileConversationStore(string root) { Directory.CreateDirectory(root); _root = root; }

    public async Task AppendMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        var ts = message.TimestampUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'");
        var role = message.Role.ToString().ToLowerInvariant();
        var seq = Interlocked.Increment(ref _seq).ToString("D4");
        var path = Path.Combine(_root, $"{ts}_{seq}_{role}.json");
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = false });
        await File.WriteAllTextAsync(path, json, ct);
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
            var msg = await JsonSerializer.DeserializeAsync<ChatMessage>(s, cancellationToken: ct);
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
    var rendered = await renderer.RenderAsync(prompt);
    lm.SystemPrompt = prompt.System; // ILanguageModelService remains unchanged
    var assistantText = await lm.GetResponseAsync(rendered);

    var assistantMsg = new ChatMessage { Role = ChatRole.Assistant, Content = assistantText, TimestampUtc = DateTimeOffset.UtcNow };
    await store.AppendMessageAsync(assistantMsg);

    // TTS (if enabled) and FSM transitions happen in host logic
};
```


## Configuration

Add to `appsettings.json` later:

```json
{
  "Context": {
    "LastN": 6,
    "StorageRoot": null
  }
}
```

- `StorageRoot = null` means use default per-OS location; otherwise use explicit path.


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
- Phase 2 (MVP): `FileConversationStore`, `LastNContextProvider`, `SystemPromptProvider`, `PromptBuilder`, `OllamaPromptRenderer`, and host wiring.
- Phase 3 (hardening): tail-read optimization, redaction hook, optional seq persistence.
- Phase 4 (RAG-ready): `IRagRetriever`/`IEmbedder` interfaces and `RagContextProvider` with a stub retriever.
- Phase 5 (providers): add provider-specific renderers that support structured chat arrays where available.


## Notes

- Keep `Announcements` (`Elara.Host/Utilities/Announcements.cs`) separate from conversation persistence unless explicitly wanted; if persisted, use `Role = System` and classify via `Metadata`.
- Unit-test recording sessions (`Elara.Host/Utilities/SessionRecording.cs`) are unrelated to conversation persistence and should not affect this architecture.


---
This file captures the shapes and integration points needed to resume work quickly. Next implementation step is Phase 2 (MVP) as outlined above, without modifying `ILanguageModelService`.
