using Eventask.ApiService.Repository;
using Eventask.Domain.Dtos;
using Eventask.Domain.Entity.Calendars;

namespace Eventask.ApiService.Endpoints;

public static partial class ApiEndpoints
{
    extension(RouteGroupBuilder api)
    {
        private RouteGroupBuilder MapSpecialDays()
        {
            var group = api.MapGroup("/special-days").WithTags("SpecialDays");

            group.MapGet("/", async (
                    DateTime from,
                    DateTime to,
                    ISpecialDayRepository repository) =>
                {
                    if (from.Date > to.Date)
                    {
                        return Results.Problem(
                            title: "Invalid request",
                            detail: "From date must be earlier than or equal to to date.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    var days = await repository.ListBetweenAsync(from, to);
                    var dtos = days.Select(day => new SpecialDayDto(day.Date, day.Type.ToString())).ToList();
                    return Results.Ok(dtos);
                })
                .RequireAuthorization()
                .WithName("SpecialDays_List")
                .Produces<IReadOnlyList<SpecialDayDto>>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status401Unauthorized);

            return group;
        }
    }
}
