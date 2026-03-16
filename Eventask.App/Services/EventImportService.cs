using System.IO;
using System.Linq;
using System.Text.Json;
using Eventask.App.Models;
using Eventask.App.Services.Generated;
using Eventask.Domain.Requests;
using Refit;

namespace Eventask.App.Services;

public class EventImportService : IEventImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IEventaskApi _api;

    public EventImportService(IEventaskApi api)
    {
        _api = api;
    }

    public Task<ImportParseResult> ParseAsync(string jsonContent)
    {
        return Task.FromResult(ParseInternal(jsonContent));
    }

    public async Task<ImportOperationResult> ImportAsync(Guid calendarId, string jsonContent, CancellationToken cancellationToken = default)
    {
        var parseResult = ParseInternal(jsonContent);

        // 如果解析阶段就有错误,记录并终止导入
        if (parseResult.Events.Count == 0)
        {
            return new ImportOperationResult(0, parseResult.Errors.Count, parseResult.Errors);
        }

        var errors = new List<ImportValidationError>(parseResult.Errors);
        var successCount = 0;

        foreach (var evt in parseResult.Events)
        {
            try
            {
                var request = new CreateScheduleItemRequest(
                    Type: "Event",
                    Title: evt.Title,
                    Description: evt.Description,
                    Location: evt.Location,
                    StartAt: evt.Start,
                    EndAt: evt.End,
                    DueAt: null,
                    AllDay: evt.AllDay
                );

                await _api.ItemsPostAsync(calendarId, request, cancellationToken);
                successCount++;
            }
            catch (ApiException apiEx)
            {
                errors.Add(new ImportValidationError(evt.Index, $"API 调用失败: {apiEx.Message}"));
            }
            catch (Exception ex)
            {
                errors.Add(new ImportValidationError(evt.Index, $"导入失败: {ex.Message}"));
            }
        }

        var failureCount = errors.Count;
        return new ImportOperationResult(successCount, failureCount, errors);
    }

    public async Task<ImportOperationResult> ImportFromFileAsync(Guid calendarId, string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            var errors = new List<ImportValidationError>
            {
                new(0, $"文件不存在: {filePath}")
            };
            return new ImportOperationResult(0, errors.Count, errors);
        }

        var jsonContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        return await ImportAsync(calendarId, jsonContent, cancellationToken);
    }

    private static ImportParseResult ParseInternal(string jsonContent)
    {
        var errors = new List<ImportValidationError>();
        List<ImportEventPayload>? payloads = null;

        // 先尝试解析 envelope 结构 { "events": [...] }
        try
        {
            var envelope = JsonSerializer.Deserialize<ImportEventsEnvelope>(jsonContent, JsonOptions);
            if (envelope?.Events is { Count: > 0 })
            {
                payloads = envelope.Events;
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ImportValidationError(0, $"JSON 解析失败: {ex.Message}"));
        }

        // 如果 envelope 失败或为空,尝试直接把根解析为数组
        if (payloads == null)
        {
            try
            {
                payloads = JsonSerializer.Deserialize<List<ImportEventPayload>>(jsonContent, JsonOptions);
            }
            catch (Exception ex)
            {
                if (!errors.Any(e => e.Index == 0))
                {
                    errors.Add(new ImportValidationError(0, $"JSON 解析失败: {ex.Message}"));
                }
            }
        }

        if (payloads == null)
        {
            return new ImportParseResult(Array.Empty<ParsedImportEvent>(), errors);
        }

        var parsed = new List<ParsedImportEvent>();

        for (int i = 0; i < payloads.Count; i++)
        {
            var idx = i + 1; // 1-based for user-friendly reporting
            var item = payloads[i];

            if (item == null)
            {
                errors.Add(new ImportValidationError(idx, "该条目为空"));
                continue;
            }

            var title = (item.Name ?? item.Title)?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                errors.Add(new ImportValidationError(idx, "缺少名称/标题"));
                continue;
            }

            var start = item.Start ?? item.StartAt;
            var end = item.End ?? item.EndAt;

            if (start == null)
            {
                errors.Add(new ImportValidationError(idx, "缺少开始时间 start/startAt"));
                continue;
            }

            if (end == null)
            {
                errors.Add(new ImportValidationError(idx, "缺少结束时间 end/endAt"));
                continue;
            }

            if (end <= start)
            {
                errors.Add(new ImportValidationError(idx, "结束时间必须晚于开始时间"));
                continue;
            }

            parsed.Add(new ParsedImportEvent(
                Index: idx,
                Title: title,
                Description: item.Description?.Trim(),
                Location: item.Location?.Trim(),
                Start: start.Value,
                End: end.Value,
                AllDay: item.AllDay ?? false
            ));
        }

        return new ImportParseResult(parsed, errors);
    }
}
