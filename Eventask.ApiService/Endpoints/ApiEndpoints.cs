using Microsoft.AspNetCore.Http.HttpResults;

namespace Eventask.ApiService.Endpoints;

public static partial class ApiEndpoints
{
    public static WebApplication MapEventaskApi(this WebApplication app)
    {
        app.MapGroup("/")
            .MapAuth()
            .MapCalendars()
            .MapScheduleItems()
            .MapAttachments()
            .MapSync();

        return app;
    }


    private static ProblemHttpResult NotImplemented()
    {
        return TypedResults.Problem(title: "Not implemented", statusCode: StatusCodes.Status501NotImplemented);
    }
}