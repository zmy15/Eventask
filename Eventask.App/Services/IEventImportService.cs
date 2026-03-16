using Eventask.App.Models;

namespace Eventask.App.Services;

public interface IEventImportService
{
    /// <summary>
    /// 仅解析和校验 JSON, 不触发导入。
    /// </summary>
    Task<ImportParseResult> ParseAsync(string jsonContent);

    /// <summary>
    /// 从字符串导入事件到指定日历。
    /// </summary>
    Task<ImportOperationResult> ImportAsync(Guid calendarId, string jsonContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// 从文件导入事件到指定日历。
    /// </summary>
    Task<ImportOperationResult> ImportFromFileAsync(Guid calendarId, string filePath, CancellationToken cancellationToken = default);
}
