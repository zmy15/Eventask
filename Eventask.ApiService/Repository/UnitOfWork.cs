using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Eventask.ApiService.Repository;

/// <summary>
/// Implementation of the Unit of Work pattern that wraps the EventaskContext.
/// Coordinates changes across multiple repositories within a single transaction.
/// </summary>
public class UnitOfWork(EventaskContext db) : IUnitOfWork
{
    private IDbContextTransaction? _currentTransaction;

    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Concurrency error. The entity has been modified by another operation.",
                ex.Entries.FirstOrDefault()?.Entity);
        }
    }

    /// <inheritdoc/>
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null) throw new InvalidOperationException("A transaction is already in progress.");

        _currentTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        return _currentTransaction;
    }

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null) throw new InvalidOperationException("No transaction in progress.");

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Concurrency error. The entity has been modified by another operation.",
                ex.Entries.FirstOrDefault()?.Entity);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null) throw new InvalidOperationException("No transaction in progress.");

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentTransaction != null) await _currentTransaction.DisposeAsync();
    }
}