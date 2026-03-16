using Eventask.Domain.Entity.Calendars;
using Microsoft.EntityFrameworkCore;

namespace Eventask.ApiService.Repository;

public interface ISpecialDayRepository
{
    Task<IReadOnlyList<SpecialDay>> ListBetweenAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}

public sealed class SpecialDayRepository(EventaskContext db) : ISpecialDayRepository
{
    public async Task<IReadOnlyList<SpecialDay>> ListBetweenAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var from = fromDate.Date;
        var to = toDate.Date;

        var query = db.SpecialDays
            .AsNoTracking()
            .Where(day => day.Date >= from && day.Date <= to)
            .OrderBy(day => day.Date);

        var results = await query.ToListAsync(cancellationToken);
        return results.AsReadOnly();
    }
}
