using GustosApp.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace GustosApp.Infraestructure.Services;

public sealed class EfApplicationTransaction : IApplicationTransaction
{
    private readonly GustosDbContext _dbContext;

    public EfApplicationTransaction(GustosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsRelational())
            return await operation(cancellationToken);

        // Serializable protege el cupo Free frente a creación/unión concurrente en dos pestañas.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
