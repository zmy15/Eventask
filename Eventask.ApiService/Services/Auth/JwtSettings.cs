namespace Eventask.ApiService.Services.Auth;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = String.Empty;
    public string Issuer { get; set; } = "Eventask";
    public string Audience { get; set; } = "Eventask";
    public int ExpirationMinutes { get; set; } = 1440; // 24 hours
}