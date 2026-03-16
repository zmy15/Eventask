using Eventask.ApiService.Endpoints;
using Eventask.ApiService.Repository;
using Eventask.ApiService.Services.Storage;
using Eventask.ApiService.Utilities;
using Eventask.Domain.Entity.Calendars;
using Eventask.Domain.Entity.Calendars.ScheduleItems;
using Eventask.Domain.Entity.Users;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.ConfigureAuth();
builder.AddNpgsqlDbContext<EventaskContext>("postgres-db",
    settings => { settings.DisableRetry = true; },
    options =>
        options
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .EnableThreadSafetyChecks());

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICalendarRepository, CalendarRepository>();
builder.Services.AddScoped<ICalendarMemberRepository, CalendarMemberRepository>();
builder.Services.AddScoped<IScheduleItemRepository, ScheduleItemRepository>();
builder.Services.AddScoped<IAttachmentRepository, AttachmentRepository>();
builder.Services.AddScoped<ISpecialDayRepository, SpecialDayRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.Configure<ObjectStorageOptions>(builder.Configuration.GetSection(ObjectStorageOptions.SectionName));
builder.Services.AddSingleton<IObjectStorageService, S3ObjectStorageService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();


if ( app.Environment.IsDevelopment() ) app.MapOpenApi();
app.MapEventaskApi();
app.MapScalarApiReference(options => options
    .AddPreferredSecuritySchemes("BearerAuth")
    .AddHttpAuthentication("BearerAuth", _ => { }));
app.MapDefaultEndpoints();

app.Run();
