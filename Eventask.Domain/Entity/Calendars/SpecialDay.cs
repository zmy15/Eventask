namespace Eventask.Domain.Entity.Calendars;

public enum SpecialDayType
{
    Rest,
    Work
}

public class SpecialDay
{
    public Guid Id { get; private set; }

    public DateTime Date { get; private set; }

    public SpecialDayType Type { get; private set; }

    private SpecialDay() { }

    public static SpecialDay Create(DateTime date, SpecialDayType type)
    {
        return new SpecialDay
        {
            Id = Guid.NewGuid(),
            Date = date.Date,
            Type = type
        };
    }
}
