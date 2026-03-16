using System.Security.Claims;
using Eventask.ApiService.Repository;
using Eventask.ApiService.Services.Auth;
using Eventask.Domain.Dtos;
using Eventask.Domain.Entity.Users;
using Eventask.Domain.Requests;

namespace Eventask.ApiService.Endpoints;

public static partial class ApiEndpoints
{
    extension(RouteGroupBuilder api)
    {
        private RouteGroupBuilder MapAuth()
        {
            var group = api.MapGroup("/auth").WithTags("Auth");

            group.MapGet("/test", (ClaimsPrincipal user) =>
            {
                var primaryIdentity = user.Identity as ClaimsIdentity;

                var payload = new
                {
                    Name = primaryIdentity?.Name,
                    NameIdentifiers = user.FindFirstValue(ClaimTypes.NameIdentifier),
                    IsAuthenticated = primaryIdentity?.IsAuthenticated ?? false,
                    AuthenticationType = primaryIdentity?.AuthenticationType,
                    Claims = user.Claims.Select(c => new
                    {
                        c.Type,
                        c.Value,
                        c.ValueType,
                        c.Issuer,
                        c.OriginalIssuer
                    }),
                    Identities = user.Identities.Select(identity => new
                    {
                        identity.Name,
                        identity.IsAuthenticated,
                        identity.AuthenticationType,
                        Claims = identity.Claims.Select(c => new
                        {
                            c.Type,
                            c.Value,
                            c.ValueType,
                            c.Issuer,
                            c.OriginalIssuer
                        })
                    })
                };

                return Results.Ok(payload);
            });

            group.MapPost("/register", async (
                    RegisterRequest request,
                    IUserRepository userRepository,
                    IJwtService jwtService,
                    IUnitOfWork unitOfWork) =>
                {
                    // Validate request
                    if (String.IsNullOrWhiteSpace(request.Username) ||
                        String.IsNullOrWhiteSpace(request.Password))
                        return Results.Problem(
                            title: "Invalid request",
                            detail: "Username, and Password are required.",
                            statusCode: StatusCodes.Status400BadRequest);

                    if (request.Password is { Length: < 6 })
                        return Results.Problem(
                            title: "Invalid request",
                            detail: "Password must be at least 6 characters.",
                            statusCode: StatusCodes.Status400BadRequest);

                    // Check if user already exists
                    if (await userRepository.ExistsByUsernameAsync(request.Username))
                        return Results.Problem(
                            title: "User already exists",
                            detail: "A user with this username already exists.",
                            statusCode: StatusCodes.Status400BadRequest);

                    // Create user using factory method
                    var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                    var user = User.Create(request.Username, passwordHash);

                    await userRepository.CreateAsync(user);
                    await unitOfWork.SaveChangesAsync();

                    // Generate token
                    var (token, expiresAt) = jwtService.GenerateToken(user);

                    return Results.Ok(new AuthResponse(token, expiresAt));
                })
                .WithName("Auth_Register")
                .Produces<AuthResponse>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest);

            group.MapPost("/login", async (
                    LoginRequest request,
                    IUserRepository userRepository,
                    IJwtService jwtService) =>
                {
                    // Validate request
                    if (String.IsNullOrWhiteSpace(request.Username) ||
                        String.IsNullOrWhiteSpace(request.Password))
                        return Results.Problem(
                            title: "Invalid request",
                            detail: "Username and Password are required.",
                            statusCode: StatusCodes.Status400BadRequest);

                    // Find user by username
                    var user = await userRepository.GetByUsernameAsync(request.Username);

                    if (user is null || user.IsDeleted)
                        return Results.Problem(
                            title: "Invalid credentials",
                            detail: "Invalid username or password.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    // Verify password
                    if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                        return Results.Problem(
                            title: "Invalid credentials",
                            detail: "Invalid username or password.",
                            statusCode: StatusCodes.Status401Unauthorized);

                    // Generate token
                    var (token, expiresAt) = jwtService.GenerateToken(user);

                    return Results.Ok(new AuthResponse(token, expiresAt));
                })
                .WithName("Auth_Login")
                .Produces<AuthResponse>(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status401Unauthorized);

            return api;
        }
    }
}