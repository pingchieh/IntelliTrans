using IntelliTrans.Core;
using IntelliTrans.Core.Extensions;
using IntelliTrans.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntelliTrans.Cli.Commands;

internal partial class MainCommands
{
    public async Task Patch(
        CancellationToken cancellationToken,
        bool skipNoDll = true,
        string savePath = "zh-Hans",
        string contentFilter = @"[\u4e00-\u9fa5]"
    )
    {
        var options = CreateFileOptions(skipNoDll, contentFilter);
        _logger.LogInformation(
            """
            Patch IntelliSense Files:
                --includeDirs:  {includeDirs}
                --excludeFiles: {excludeFiles}
                --skipNoDll:    {skipNoDll}
                --savePath:     {savePath}
                --contentFilter:  {contentFilter}
            """,
            options.IncludeDirs,
            options.ExcludeFiles,
            options.SkipNoDll,
            savePath,
            options.ContentFilter
        );

        foreach (string dir in options.IncludeDirs)
        {
            if (!Directory.Exists(dir))
            {
                _logger.LogWarning("目录不存在：{dir}", dir);
                continue;
            }

            string[] xmlFiles = Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories);
            foreach (string xmlFile in xmlFiles)
            {
                string saveFile = Path.Combine(
                    Path.GetDirectoryName(xmlFile)!,
                    savePath,
                    Path.GetFileName(xmlFile)
                );

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (File.Exists(saveFile) && saveFile != xmlFile)
                {
                    continue;
                }

                if (
                    !Environment.IsPrivilegedProcess
                    && xmlFile.StartsWith(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    _logger.LogWarning("当前文件需要管理员权限，已跳过！{xmlFile}", xmlFile);
                    continue;
                }

                if (ShouldSkipXmlFile(xmlFile, options))
                {
                    continue;
                }

                var file = IntelliSenseFile.Parse(xmlFile);
                if (file == null || (file.IsTranslated() && saveFile == xmlFile))
                {
                    continue;
                }

                _logger.LogInformation("Processing {xmlFile}", xmlFile);
                var hashes = file.GetContentsByTags(XmlDocTags)
                    .Where(c => !c.IsNullOrWhiteSpace() && !c.IsRegexMatch(options.ContentFilter))
                    .Select(c => c.ReplacExtraSpaces("").CalculateMd5())
                    .Distinct();
                if (!hashes.Any())
                {
                    continue;
                }

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IntelliSenseDbContext>();
                var translations = await dbContext
                    .Translations.Where(t => hashes.Contains(t.OriginalHash))
                    .ToDictionaryAsync(t => t.OriginalHash, t => t.Content, cancellationToken);
                if (translations.Count == 0)
                {
                    continue;
                }

                var allXmlElements = file.GetXmlElementsByTags(XmlDocTags);
                foreach (var xmlElement in allXmlElements)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    string key = xmlElement.InnerXml.ReplacExtraSpaces("").CalculateMd5();
                    if (!translations.TryGetValue(key, out string? translation))
                    {
                        continue;
                    }

                    xmlElement.InnerXml = translation;
                }

                file.SaveXml(saveFile);
            }
        }
    }
}
