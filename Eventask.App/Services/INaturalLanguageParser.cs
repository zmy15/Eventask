using Eventask.App.Models;

namespace Eventask.App.Services;

public interface INaturalLanguageParser
{
    /// <summary>
    /// 将自然语言描述解析成多个日程或任务草稿。
    /// </summary>
    /// <param name="prompt">用户输入的自然语言描述。</param>
    /// <param name="referenceDate">参考日期,用于补全缺失的日期信息。</param>
    /// <param name="cancellationToken">取消标记。</param>
    /// <returns>识别出的日程草稿列表。</returns>
    Task<IReadOnlyList<RecognizedScheduleDraft>> ParseAsync(
        string prompt,
        DateTime referenceDate,
        CancellationToken cancellationToken = default);
}
