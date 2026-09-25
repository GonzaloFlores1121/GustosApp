namespace GustosApp.Application.Interfaces;

public interface IApplicationTransaction
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

public sealed class ImmediateApplicationTransaction : IApplicationTransaction
{
    public static ImmediateApplicationTransaction Instance { get; } = new();

    private ImmediateApplicationTransaction() { }

    public Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) => operation(cancellationToken);
}
