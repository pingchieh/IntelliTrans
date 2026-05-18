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
            List<IntelliSenseOriginal> originals = [];
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    originals = await dbContext
                        .Originals.Include(o => o.Translations)
                        .Where(o =>
                            o.Id > lastid && !o.Translations.Any(t => t.Language == language)
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

            if (originals.Count == 0)
            {
                break;
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

                    ChatCompletionOptions chatCompletionOptions = new()
                    {
                        Temperature = temperature,
                        EndUserId = userid,
                    };
                    List<ChatMessage> messages = Prompts.CreateTranslatePrompt(
                        language,
                        original.Content
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
                        _logger.LogWarning("翻译失败(输出错误)：{response}", response);
                        return;
                    }

                    var translation = response.RegexReplace(@"^```[^\n]*\n([\s\S]*?)```$", "$1");

                    if (IntelliSenseFile.IsValidXml(translation))
                    {
                        _logger.LogInformation(
                            "翻译成功：\n\t原文：{origion}\n\n\t译文：{translation}",
                            original.Content,
                            translation
                        );
                        original.Translations.Add(
                            new IntelliSenseTranslation
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
                    }
                }
            );

            dbContext.UpdateRange(originals);
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SaveChanges Error.");
                }
            }
        }
    }
}
