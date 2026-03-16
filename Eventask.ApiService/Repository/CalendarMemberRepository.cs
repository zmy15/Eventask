using Eventask.Domain.Entity.Calendars;

namespace Eventask.ApiService.Repository;

public class CalendarMemberRepository(EventaskContext db) : ICalendarMemberRepository
{
    public async Task AddTrackingAsync(CalendarMember member)
    {
        db.Add(member);
    }
}