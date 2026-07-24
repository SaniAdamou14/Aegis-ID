namespace Aegis.Graph;

/// <summary>Abstraction over waiting between retries, so tests can run without real delays.</summary>
public interface IRetryDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class SystemRetryDelay : IRetryDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);
}
