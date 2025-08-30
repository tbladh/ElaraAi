namespace Elara.Core.Time;

/// <summary>
/// Minimal abstraction over time to enable deterministic testing.
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Current UTC time.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
