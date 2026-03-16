using Eventask.Domain.Dtos;
using Eventask.Domain.Requests;

namespace Eventask.ApiService.Endpoints;

public static partial class ApiEndpoints
{
    extension(RouteGroupBuilder api)
    {
        private RouteGroupBuilder MapSync()
        {
            var group = api.MapGroup("/sync").WithTags("Sync");

            group.MapPost("/pull", (SyncPullRequest request) => NotImplemented())
                .RequireAuthorization()
                .WithName("Sync_Pull")
                .Produces<SyncPullResponse>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status501NotImplemented);

            group.MapPost("/push", (SyncPushRequest request) => NotImplemented())
                .RequireAuthorization()
                .WithName("Sync_Push")
                .Produces<SyncPushResponse>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status501NotImplemented);

            return api;
        }
    }
}