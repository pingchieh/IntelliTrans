using System.ClientModel;
using System.Reflection;
// 添加System.Threading.Tasks命名空间支持
using IntelliTrans.Core;
using IntelliTrans.Core.Extensions;
using IntelliTrans.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace IntelliTrans.Cli.Commands;

internal partial class MainCommands
{
    /// <summary>
    /// 使用 OpenAI API 将 IntelliSense 文件的原文内容翻译成指定语言。
    /// </summary>
    /// <param name="apiUrl">OpenAI API 的 URL。如果未提供，则从配置中获取。</param>
    /// <param name="apiKey">OpenAI API 的密钥。如果未提供，则从配置中获取。</param>
    /// <param name="model">OpenAI 使用的模型名称。如果未提供，则从配置中获取。</param>
    /// <param name="language">目标语言，默认为简体中文。</param>
    /// <param name="parallelism">并行处理数量，默认为8。</param>
    /// <returns>一个代表异步操作的任务。</returns>
    public async Task Translate(
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

        var originals = _dbContext
            .Originals.Include(o => o.Translations)
            .Where(o => !o.Translations.Any(t => t.Language == language))
            .OrderBy(o => o.Id);

        var skipList = new List<string>();
        do
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            // 并行处理翻译任务
            await Parallel.ForEachAsync(
                originals.Where(o => !skipList.Contains(o.Hash)).Take(20 * parallelism),
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
                    if (skipList.Contains(original.Hash))
                    {
                        return;
                    }
                    ChatCompletionOptions chatCompletionOptions = new()
                    {
                        Temperature = temperature,
                        EndUserId = userid,
                    };
                    var messages = CreatePrompt(language, original.Content);
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
                        skipList.Add(original.Hash);
                        return;
                    }
                    if (response.IsNullOrWhiteSpace())
                    {
                        _logger.LogWarning("OpenAI Response is empty.");
                        return;
                    }
                    if (!(response.StartsWith("```xml") && response.EndsWith("```")))
                    {
                        _logger.LogWarning("翻译失败(输出错误)：{response}", response);
                        skipList.Add(original.Hash);
                        return;
                    }

                    string translation = response.RegexReplace(@"^```[^\n]*\n([\s\S]*?)```$", "$1");

                    if (IntelliSenseFile.IsValidXml(translation))
                    {
                        _logger.LogInformation(
                            "翻译成功：\n\t原文：{origion}\n\n\t译文：{translation}",
                            original.Content,
                            translation
                        );
                        original.Translations.Add(
                            new IntelliSenseTranslation()
                            {
                                Content = translation,
                                OriginalHash = original.Hash,
                                Language = language,
                            }
                        );
                    }
                    else
                    {
                        _logger.LogWarning("翻译失败(Xml格式错误)：{translation}", translation);
                        skipList.Add(original.Hash);
                    }
                }
            );
            await _dbContext.SaveChangesAsync(cancellationToken);
        } while (originals.Count() > skipList.Count);
    }

    /// <summary>
    /// 创建一个提示消息列表，用于将Microsoft .NET SDK IntelliSense的文档翻译为指定语言。
    /// </summary>
    /// <param name="language">目标语言。</param>
    /// <param name="originalText">需要翻译的原始文本。</param>
    /// <returns>包含提示消息的ChatMessage列表。</returns>
    private static List<ChatMessage> CreatePrompt(string language, string originalText)
    {
        return
        [
            new SystemChatMessage(
                $"你是一名专业的.Net软件工程师，你熟悉 C#/.Net 的各种专业术语，现在你需要将Microsoft .NET SDK IntelliSense的文档翻译为{language}。"
            ),
            new UserChatMessage(
                $$"""
                将以下 XML 内容翻译为{{language}}，确保严格遵循以下要求：

                ### 翻译要求：
                - **目标语言**：{{language}}。
                - **意义准确**：确保翻译准确传达原文含义，避免任何歧义或误解。
                - **专业术语**：使用准确的专业术语，确保技术文档的专业性。

                ### 格式要求：
                - 确保翻译后的 Xml 结构与原文一致，例如标签和属性（如 `<see cref="T:System.Type"/>`）。
                - 使用`{ }`包裹的内容保持不变。
                - 使用标签包裹的内容，例如`<c> </c>`包裹的内容保持不变。

                ### 输入结构：
                - 使用Markdown的代码块包裹起来的Xml字符串。

                ### 输出结构：
                - 将翻译的结果使用Markdown的代码块包裹起来。

                ### 翻译步骤：
                1. 仔细阅读原文，理解文本的上下文和技术含义。
                2. 仅翻译 XML 标签之间的文本内容，保持标签和属性不变。
                3. 确保翻译后的文本流畅、准确，符合{{language}}表达习惯。
                4. 确保翻译后的文本符合Xml规范，不会引发错误。
                5. 仅输出用代码块包裹翻译结果，不要添加任何其它内容。

                ### 示例输入1：
                ```xml
                The <see cref="T:System.Type"/> that indicates where this operation is used.
                ```

                ### 示例输出1：
                ```xml
                指示此操作所使用的<see cref="T:System.Type"/>。
                ```

                ### 示例输入2：
                ```xml
                The entity type '{entityType}' is mapped to the 'DbFunction' named '{functionName}' with return type '{returnType}'. Ensure that the mapped function returns 'IQueryable&lt;{clrType}&gt;'
                ```

                ### 示例输出2：
                ```xml
                实体类型'{entityType}'被映射到名为'{functionName}'的'DbFunction'，返回类型为'{returnType}'。请确保映射的函数返回'IQueryable&lt;{clrType}&gt;'
                ```

                ### 输入：

                ```xml
                {{originalText}}
                ```

                ### 输出：
                """
            ),
        ];
    }
}
