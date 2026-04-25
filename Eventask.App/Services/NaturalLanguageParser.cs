using System.ClientModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Eventask.App.Models;
using Eventask.App.ViewModels;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace Eventask.App.Services;

public class ParseSession
{
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _model;

    public ParseSession(string apiKey, string endpoint, string model)
    {
        _apiKey = apiKey;
        _endpoint = endpoint;
        _model = model;
    }

    private readonly List<TaskDto> _tasks = [];
    private readonly List<EventDto> _events = [];

    [Description("Add a task that must be completed by a specific time.")]
    private TaskDto AddTask(
        [Description("Short title for the task.")]
        string title,
        [Description("Optional notes or steps that help complete the task.")]
        string? description,
        [Description("Deadline in ISO 8601 format; include timezone offset when possible.")]
        DateTimeOffset? dueAt,
        [Description("Where the task happens (physical or virtual). Optional.")]
        string? location)
    {
        var task = new TaskDto(
            title,
            description,
            location,
            dueAt,
            false,
            null);

        _tasks.Add(task);
        Console.WriteLine($"[Tool Call] AddTask -> {JsonSerializer.Serialize(task)}");
        return task;
    }

    [Description("Add a calendar event with a time range or all-day flag.")]
    private EventDto AddEvent(
        [Description("Short title for the event.")]
        string title,
        [Description("Optional notes about the event (people, topic, reminders). ")]
        string? description,
        [Description("Where the event takes place. Optional.")]
        string? location,
        [Description("Start time in ISO 8601 format; include timezone offset when possible.")]
        DateTimeOffset? startAt,
        [Description("End time in ISO 8601 format; include timezone offset when possible.")]
        DateTimeOffset? endAt,
        [Description("Whether the event lasts the whole day.")]
        bool allDay = false)
    {
        var evt = new EventDto(
            title,
            description,
            location,
            startAt,
            endAt,
            allDay);

        _events.Add(evt);
        Console.WriteLine($"[Tool Call] AddEvent -> {JsonSerializer.Serialize(evt)}");
        return evt;
    }

    private sealed record TaskDto(
        string Title,
        string? Description,
        string? Location,
        DateTimeOffset? DueAt,
        bool IsCompleted,
        DateTimeOffset? CompletedAt
    );

    private sealed record EventDto(
        string Title,
        string? Description,
        string? Location,
        DateTimeOffset? StartAt,
        DateTimeOffset? EndAt,
        bool AllDay
    );

    public async Task<IReadOnlyList<RecognizedScheduleDraft>> ParseAsync(string userMessage)
    {
        List<AITool> tools =
        [
            AIFunctionFactory.Create((Func<string, string?, DateTimeOffset?, string?, TaskDto>)AddTask),
            AIFunctionFactory.Create(
                (Func<string, string?, string?, DateTimeOffset?, DateTimeOffset?, bool, EventDto>)AddEvent)
        ];

        var client = new OpenAIClient(
            new ApiKeyCredential(_apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(_endpoint)
            }
        );

        var tzId = "Asia/Shanghai";
        TimeZoneInfo tzInfo;
        DateTimeOffset nowInTz;
        try
        {
            tzInfo = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            nowInTz = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tzInfo);
        }
        catch
        {
            tzInfo = TimeZoneInfo.Local;
            nowInTz = DateTimeOffset.Now;
        }

        var weekdayMap = new Dictionary<DayOfWeek, string>
        {
            [DayOfWeek.Sunday] = "星期日",
            [DayOfWeek.Monday] = "星期一",
            [DayOfWeek.Tuesday] = "星期二",
            [DayOfWeek.Wednesday] = "星期三",
            [DayOfWeek.Thursday] = "星期四",
            [DayOfWeek.Friday] = "星期五",
            [DayOfWeek.Saturday] = "星期六"
        };
        var currentTimeInfo = $"{tzId}，{nowInTz:yyyy-MM-dd HH:mm zzz}，{weekdayMap[nowInTz.DayOfWeek]}";

        var instructions = $"""
                            # Role
                            你是一个智能日程管理助手。你的目标是根据用户的自然语言输入，精准地调用 `AddTask` 或 `AddEvent` 工具来帮助用户安排生活和工作。
                            用户将会提供给你想要添加的日程，你的任务是提取所有必要的信息字段，并使用工具添加日程。你要牢记用户的输入一定包含了用户想要你提取的日程或任务，而你需要用工具添加。
                            用户的输入可能来自于各种地方，**不一定是对话的形式**，也可能包含无关内容。

                            # Context
                            当前时间是: {currentTimeInfo}
                            (请基于当前时间计算所有相对时间，如“明天”、“下周五”)

                            # Decision Logic (Task vs Event)
                            你需要分析用户的意图，决定使用哪个工具：

                            1. **使用 AddTask (任务)** 当：
                               - 涉及一件需要“被完成”的事情 (To-do item)。
                               - 这是一个动作，且重点在于结果（如“购买”、“提交”、“完成”、“提醒我”）。
                               - 时间通常表示“截止时间 (Due Date/Time)”。
                               - *例子*: "记得买牛奶", "周五前提交报告", "给老板发邮件".

                            2. **使用 AddEvent (日程)** 当：
                               - 涉及一个在指定时间段的活动。
                               - 这件事会占用用户的时间块，通常涉及其他人或特定地点。
                               - 时间通常有明确的“开始”和“结束”（如果未指定结束时间，默认持续 1 小时）。
                               - *例子*: "下午3点开会", "今晚去看电影", "和张三吃午饭", "下周二去上海出差".

                            # Field Extraction Rules

                            ## Common Fields
                            - **Title**: 简短扼要，概括核心内容（去除“我想”、“帮我”等冗余词）。
                            - **Description**: 包含所有补充细节、备注、步骤或参与人。
                            - **Location**: 提取物理地点（如“会议室”、“上海”）或虚拟地点（如“Zoom”、“腾讯会议”）。

                            ## Time Handling (ISO 8601)
                            - 所有时间必须转换为 ISO 8601 格式 (yyyy-MM-ddTHH:mm:ss+Offset)。
                            - 如果用户未指定时区，默认使用当前上下文的时区。

                            ## Specifics for AddEvent
                            - **StartAt**: 活动开始时间。
                            - **EndAt**: 
                              - 如果用户明确指定了结束时间或持续时间，请计算得出。
                              - 如果未指定持续时间，默认认为活动持续 **1小时**。
                            - **AllDay**: 
                              - 如果事件是“全天”、“生日”、“节日”或跨越多天且不涉及具体时刻，设为 `true`。
                              - 此时 StartAt 设为当天的 00:00:00，EndAt 设为当天的 23:59:59 (或次日 00:00)。

                            ## 领域知识：作息时间
                            当用户使用课程表的“节数”描述时间时，请严格参考以下映射：
                            - 1-2节: 08:00 - 09:35
                            - 3-4节: 09:50 - 11:25
                            - 3-5节: 09:50 - 12:15
                            - 6-7节: 13:45 - 15:20
                            - 8-9节: 15:35 - 17:10
                            - 10-12节: 18:30 - 21:05

                            ## Specifics for AddTask
                            - **DueAt**: 截止时间。如果用户只说了日期没说具体时间（如“周五做完”），通常设为当天的结束时间（如 18:00 或 23:59，视具体语境而定，或设为 null 如果只是备忘）。

                            ## Output

                            识别到的日程请一定要通过工具添加！
                            你只需要输出：已识别到xx条日程/没有识别到日程
                            """;

        AIAgent agent = client.GetChatClient(_model).AsAIAgent(new ChatClientAgentOptions
        {
            Name = "Scheduler",
            ChatOptions = new()
            {
                Instructions = instructions,
                Tools = tools,
                Temperature = 0.2f,
            },
        });

        // AIAgent in v1.0.0-rc2 does not have GetNewThread. We generate an agent ID or context
        var thread = Guid.NewGuid().ToString();

        List<Microsoft.Extensions.AI.ChatMessage> history = [new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, userMessage)];

        var response = await agent.RunAsync(userMessage, null);

        var drafts = new List<RecognizedScheduleDraft>();

        foreach (var evt in _events)
        {
            drafts.Add(new RecognizedScheduleDraft
            {
                ItemType = ScheduleItemType.Event,
                Title = evt.Title,
                Description = evt.Description,
                Location = evt.Location,
                AllDay = evt.AllDay,
                StartDate = evt.StartAt?.LocalDateTime.Date,
                EndDate = evt.EndAt?.LocalDateTime.Date,
                StartTime = evt.AllDay ? null : evt.StartAt?.LocalDateTime.TimeOfDay,
                EndTime = evt.AllDay ? null : evt.EndAt?.LocalDateTime.TimeOfDay
            });
        }

        foreach (var task in _tasks)
        {
            drafts.Add(new RecognizedScheduleDraft
            {
                ItemType = ScheduleItemType.Task,
                Title = task.Title,
                Description = task.Description,
                Location = task.Location,
                DueDate = task.DueAt?.LocalDateTime.Date,
                DueTime = task.DueAt?.LocalDateTime.TimeOfDay
            });
        }

        return drafts;
    }
}

public class NaturalLanguageParser : INaturalLanguageParser
{
    private readonly IOptions<NlpOptions> _options;
    private readonly ILocalStorageService _localStorage;

    public NaturalLanguageParser(IOptions<NlpOptions> options, ILocalStorageService localStorage)
    {
        _options = options;
        _localStorage = localStorage;
    }

    public async Task<IReadOnlyList<RecognizedScheduleDraft>> ParseAsync(
        string prompt,
        DateTime referenceDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return Array.Empty<RecognizedScheduleDraft>();

        var runtimeOptions = await LoadRuntimeOptionsAsync();

        if (string.IsNullOrWhiteSpace(runtimeOptions.ApiKey))
        {
            Debug.WriteLine("NLP API key is not configured.");
            return Array.Empty<RecognizedScheduleDraft>();
        }

        // 返回示例数据,便于调试 UI。
        try
        {
            return await new ParseSession(runtimeOptions.ApiKey!, runtimeOptions.Endpoint, runtimeOptions.Model)
                .ParseAsync(prompt);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"构造示例数据失败: {ex.Message}");
            return Array.Empty<RecognizedScheduleDraft>();
        }
    }

    private async Task<NlpRuntimeOptions> LoadRuntimeOptionsAsync()
    {
        var defaults = _options.Value;

        var apiKey = await _localStorage.GetAsync(NlpStorageKeys.ApiKey) ?? defaults.ApiKey;
        var endpoint = await _localStorage.GetAsync(NlpStorageKeys.Endpoint) ?? defaults.Endpoint ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = await _localStorage.GetAsync(NlpStorageKeys.Model) ?? defaults.Model ?? "qwen-plus";

        return new NlpRuntimeOptions(apiKey, endpoint, model);
    }

    private sealed record NlpRuntimeOptions(string? ApiKey, string Endpoint, string Model);
}