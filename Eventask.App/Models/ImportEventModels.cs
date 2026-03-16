using System.Text.Json.Serialization;

namespace Eventask.App.Models;

public class ImportEventsEnvelope
{
    [JsonPropertyName("events")]
    public List<ImportEventPayload> Events { get; set; } = new();
}

public class ImportEventPayload
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    // 兼容 "title" 字段
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("start")]
    public DateTimeOffset? Start { get; set; }

    [JsonPropertyName("startAt")]
    public DateTimeOffset? StartAt { get; set; }

    [JsonPropertyName("end")]
    public DateTimeOffset? End { get; set; }

    [JsonPropertyName("endAt")]
    public DateTimeOffset? EndAt { get; set; }

    [JsonPropertyName("allDay")]
    public bool? AllDay { get; set; }
}

public sealed record ParsedImportEvent(
    int Index,
    string Title,
    string? Description,
    string? Location,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool AllDay
);

public sealed record ImportValidationError(
    int Index,
    string Message
);

public sealed record ImportParseResult(
    IReadOnlyList<ParsedImportEvent> Events,
    IReadOnlyList<ImportValidationError> Errors
);

public sealed record ImportOperationResult(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<ImportValidationError> Errors
);
