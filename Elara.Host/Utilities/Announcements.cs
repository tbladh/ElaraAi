using System;
using System.Collections.Generic;
using System.Globalization;

namespace Elara.Host.Utilities;

/// <summary>
/// Provides short announcement phrases for various moments in the pipeline,
/// returning a random phrase per access. Backing phrases can be supplied from configuration later.
/// </summary>
public sealed class Announcements
{
    private readonly string[] _ackWakeWord;
    private readonly string[] _ackPrompt;
    private readonly string[] _ackQuiescence;
    private readonly string[] _startupTemplates;

    /// <summary>
    /// Constructs an Announcements helper.
    /// If any provided list is null or empty, sensible defaults are used.
    /// </summary>
    public Announcements(IEnumerable<string>? acknowledgeWakeWord = null,
                         IEnumerable<string>? acknowledgePrompt = null,
                         IEnumerable<string>? acknowledgeQuiescence = null,
                         IEnumerable<string>? startupTemplates = null)
    {
        _ackWakeWord = ToArrayOrDefaults(acknowledgeWakeWord, DefaultAcknowledgeWakeWord);
        _ackPrompt = ToArrayOrDefaults(acknowledgePrompt, DefaultAcknowledgePrompt);
        _ackQuiescence = ToArrayOrDefaults(acknowledgeQuiescence, DefaultAcknowledgeQuiescence);
        _startupTemplates = ToArrayOrDefaults(startupTemplates, DefaultStartupTemplates);
    }

    /// <summary>
    /// Creates an instance from options typically bound from configuration.
    /// </summary>
    public static Announcements FromOptions(AnnouncementsOptions? options)
        => new(options?.AcknowledgeWakeWord, options?.AcknowledgePrompt, options?.AcknowledgeQuiescence, options?.StartupTemplates);

    /// <summary>
    /// Returns a short random acknowledgement that the wake word was heard.
    /// </summary>
    public string AcknowledgeWakeWord => Pick(_ackWakeWord);

    /// <summary>
    /// Returns a short random acknowledgement prior to LLM processing.
    /// </summary>
    public string AcknowledgePrompt => Pick(_ackPrompt);

    /// <summary>
    /// Returns a short random acknowledgement prior to LLM processing.
    /// </summary>
    public string AcknowledgeQuiescence => Pick(_ackQuiescence);

    /// <summary>
    /// Renders a single deterministic startup announcement including key configuration values.
    /// </summary>
    public string RenderStartup(string wakeWord, string modelName, string modelBaseUrl, string? ttsVoice)
    {
        var voice = string.IsNullOrWhiteSpace(ttsVoice) ? "default voice" : ttsVoice;
        // Choose template (random, but deterministic if only one exists)
        var template = _startupTemplates.Length > 0 ? Pick(_startupTemplates) : DefaultStartupTemplates[0];
        // Safe placeholder replacement; missing placeholders are ignored
        string result = template
            .Replace("{WakeWord}", wakeWord ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{ModelName}", modelName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{ModelBaseUrl}", modelBaseUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Voice}", voice ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    private static string Pick(IReadOnlyList<string> items)
    {
        if (items.Count == 0) return string.Empty;
        var idx = Random.Shared.Next(items.Count);
        return items[idx];
    }

    private static string[] ToArrayOrDefaults(IEnumerable<string>? source, string[] defaults)
    {
        if (source == null) return defaults;
        if (source is string[] arr && arr.Length > 0) return arr;
        var list = new List<string>();
        foreach (var s in source)
        {
            var t = s?.Trim();
            if (!string.IsNullOrEmpty(t)) list.Add(t);
        }
        return list.Count > 0 ? list.ToArray() : defaults;
    }

    // Default phrases (feel free to expand over time)
    private static readonly string[] DefaultAcknowledgeWakeWord = new[]
    {
        "I'm feeling purple.",
        "I feel funny." 
    };

    private static readonly string[] DefaultAcknowledgePrompt = new[]
    {
        "Boring",
        "Why do I have to do all the work.",
    };

    private static readonly string[] DefaultAcknowledgeQuiescence = new[]
    {
        "No one wants to talk to me",
    };

    private static readonly string[] DefaultStartupTemplates = new[]
    {
        "Starting Elara. Wake word: '{WakeWord}'. Model: {ModelName} at {ModelBaseUrl}. Voice: '{Voice}'. Say the wake word to begin."
    };

}

/// <summary>
/// Configuration options for Announcements. Bind this to a configuration section, e.g. "Announcements".
/// </summary>
public sealed class AnnouncementsOptions
{
    /// <summary>Randomized phrases acknowledging the wake word.</summary>
    public IEnumerable<string>? AcknowledgeWakeWord { get; init; }
    /// <summary>Randomized phrases acknowledging a prompt before LLM processing.</summary>
    public IEnumerable<string>? AcknowledgePrompt { get; init; }

    public IEnumerable<string>? AcknowledgeQuiescence { get; init; }

    /// <summary>
    /// List of startup templates to randomly select from. If both this and StartupTemplate are null/empty,
    /// defaults will be used.
    /// </summary>
    public IEnumerable<string>? StartupTemplates { get; init; }
}
