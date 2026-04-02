using System.ClientModel;
using System.Collections.Concurrent;
using System.Reflection;
// 添加System.Threading.Tasks命名空间支持
using IntelliTrans.Core;
using IntelliTrans.Core.Extensions;
using IntelliTrans.Database;
using IntelliTrans.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace IntelliTrans.Cli.Commands;

internal partial class MainCommands
{
    /// <summary>
    /// 优化指定语言的译文
    /// </summary>
    public async Task Optimize(
        CancellationToken cancellationToken,
        string? apiUrl = null,
        string? apiKey = null,
        string? model = null,
        float temperature = 0,
        string language = "简体中文",
        int parallelism = 8
    )
    {
        apiUrl ??=
            _configuration["Openai:Endpoint"] ?? throw new ArgumentNullException(nameof(apiUrl));
        apiKey ??=
            _configuration["Openai:ApiKey"] ?? throw new ArgumentNullException(nameof(apiKey));
        model ??= _configuration["Openai:Model"] ?? throw new ArgumentNullException(nameof(model));
        string? userid = Assembly.GetExecutingAssembly().GetName().Name;
        var client = new ChatClient(
            model: model,
            credential: new ApiKeyCredential(apiKey),
            options: new OpenAIClientOptions { Endpoint = new Uri(apiUrl) }
        );
        int lastid = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IntelliSenseDbContext>();
            List<IntelliSenseOriginal> list = new();
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    list = await dbContext
                        .Originals.Include(o => o.Translations)
                        .Where(o =>
                            o.Id > lastid
                            && o.Translations.Any(t => t.Language == language && !t.IsOptimized)
                        )
                        .OrderBy(o => o.Id)
                        .Take(20 * parallelism)
                        .ToListAsync();
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during database query.");
                }
            }

            var originals = list.Select(o => new
                {
                    o.Hash,
                    o.Content,
                    Translation = o.Translations.First(t =>
                        t.Language == language && !t.IsOptimized
                    ),
                    o.Id,
                })
                .ToList();
            if (originals.Count == 0)
            {
                return;
            }
            lastid = originals.Last().Id;
            // 并行处理翻译任务
            await Parallel.ForEachAsync(
                originals,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = parallelism,
                    CancellationToken = cancellationToken,
                },
                async (original, ct) =>
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    var translation = original.Translation;
                    ChatCompletionOptions chatCompletionOptions = new()
                    {
                        Temperature = temperature,
                        EndUserId = userid,
                    };
                    var messages = CreateOptimizePrompt(
                        language,
                        original.Content,
                        translation.Content
                    );
                    string response;
                    try
                    {
                        var completion = await client.CompleteChatAsync(
                            messages,
                            chatCompletionOptions,
                            ct
                        );
                        response = completion.Value.Content[0].Text.Trim();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "OpenAI API Error.");
                        return;
                    }
                    if (response.IsNullOrWhiteSpace())
                    {
                        _logger.LogWarning("OpenAI Response is empty.");
                        return;
                    }
                    if (!(response.StartsWith("```xml") && response.EndsWith("```")))
                    {
                        _logger.LogWarning("优化翻译失败(输出错误)：{response}", response);
                        return;
                    }

                    string translationText = response.RegexReplace(
                        @"^```[^\n]*\n([\s\S]*?)```$",
                        "$1"
                    );

                    if (IntelliSenseFile.IsValidXml(translationText))
                    {
                        _logger.LogInformation(
                            "优化翻译成功：\n\t原文：{origion}\n\n\t原始译文：{originalTranslation}\n\n\t优化译文：{translation}",
                            original.Content,
                            translation.Content,
                            translationText
                        );
                        translation.Content = translationText;
                        translation.IsOptimized = true;
                        translation.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _logger.LogWarning("翻译失败(Xml格式错误)：{translation}", translationText);
                    }
                }
            );
            var translations = originals.Select(o => o.Translation);
            dbContext.UpdateRange(translations);
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during database update.");
                }
            }
        }
    }

    /// <summary>
    /// 创建用于优化提示的聊天消息列表。根据指定的目标语言、原始 XML 文本和初始翻译文本，生成系统消息和用户消息，以指导翻译编辑器进行优化。
    /// </summary>
    /// <param name="language">目标语言的名称，例如 "中文"、"日语" 等。</param>
    /// <param name="originalText">待翻译的原始 XML 文档片段。</param>
    /// <param name="translationText">对应的初始翻译 XML 文档片段。</param>
    /// <returns>包含系统消息和用户消息的 <see cref="List{ChatMessage}"/>，用于后续的翻译质量优化。</returns>
    private static List<ChatMessage> CreateOptimizePrompt(
        string language,
        string originalText,
        string translationText
    )
    {
        return
        [
            new SystemChatMessage(
                $"你是一名专业的翻译编辑,精通English和{language}，擅长将技术文档翻译成自然流畅的{language}。"
            ),
            new UserChatMessage(
                $$"""
                下面是Microsoft .NET SDK IntelliSense的XML文档词条的一部分原文和其初始翻译,请根据你的专业知识对翻译进行优化。

                按照以下要求对翻译进行优化：

                ### 格式要求：
                - 确保翻译后的 Xml 结构与原文一致，例如标签和属性（如 `<see cref="T:System.Type"/>`）。
                - 使用`{ }`包裹的内容保持不变。
                - 使用标签包裹的内容，例如`<c> </c>`包裹的内容保持不变。

                ### 步骤：
                1. 仔细阅读原文和初始翻译，确认翻译的语义与原文一致。
                2. 翻译文本的遣词造句要专业流畅,没有机翻的生硬感。
                3. 确保翻译后的文本格式正确,符合上面的格式要求。
                4. 以上步骤可重复多次，直到翻译质量达到最佳。

                ### 输出要求：
                将优化后的翻译放在xml代码块中，不包含其它内容。
                """
            ),
            new UserChatMessage(
                """
                原文：
                ```xml
                Converts instances of <see cref="T:System.Windows.Input.InputScopeName" /> to and from other data types.
                ```

                初始翻译：
                ```xml
                将<see cref="T:System.Windows.Input.InputScopeName" />的实例转换为其他数据类型，或将其他数据类型转换为其实例。
                ```
                """
            ),
            new AssistantChatMessage(
                """
                ```xml
                在<see cref="T:System.Windows.Input.InputScopeName" />实例与其他数据类型之间进行双向转换。
                ```
                """
            ),
            new UserChatMessage(
                $"""
                原文：
                ```xml
                {originalText}
                ```

                初始翻译：
                ```xml
                {translationText}
                ```
                """
            ),
        ];
    }
}
