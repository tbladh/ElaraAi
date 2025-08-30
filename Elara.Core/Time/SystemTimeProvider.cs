namespace Elara.Core.Time;

/// <summary>
/// Default production time provider using system clock.
/// </summary>
public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
