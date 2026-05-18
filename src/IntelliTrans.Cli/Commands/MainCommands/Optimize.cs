using System.ClientModel;
using System.Reflection;
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

        var userid = Assembly.GetExecutingAssembly().GetName().Name;
        var client = new ChatClient(
            model: model,
            credential: new ApiKeyCredential(apiKey),
            options: new OpenAIClientOptions { Endpoint = new Uri(apiUrl) }
        );

        var lastid = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IntelliSenseDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<IntelliSenseDbContext>();
            List<IntelliSenseOriginal> list = [];
            for (var i = 0; i < 3; i++)
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

                    IntelliSenseTranslation translation = original.Translation;
                    ChatCompletionOptions chatCompletionOptions = new()
                    {
                        Temperature = temperature,
                        EndUserId = userid,
                    };
                    List<ChatMessage> messages = Prompts.CreateOptimizePrompt(
                        language,
                        original.Content,
                        translation.Content
                    );
                    string response;
                    try
                    {
                        ClientResult<ChatCompletion> completion = await client.CompleteChatAsync(
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

                    var translationText = response.RegexReplace(
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

            IEnumerable<IntelliSenseTranslation> translations = originals.Select(o =>
                o.Translation
            );
            dbContext.UpdateRange(translations);
            for (var i = 0; i < 3; i++)
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
}
