using IntelliTrans.Core;
using IntelliTrans.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IntelliTrans.Cli.Commands;

internal partial class MainCommands
{
    /// <summary>
    /// 对IntelliSense文件进行修补，使用数据库中的翻译更新XML文档。
    /// </summary>
    /// <param name="cancellationToken">取消操作的标记。</param>
    /// <param name="includeDirs">包含XML文件的目录数组。默认为配置文件中的 "IntelliSense:IncludeDirs"。</param>
    /// <param name="excludeFiles">要排除的XML文件名数组。默认为配置文件中的 "IntelliSense:ExcludeFiles"。</param>
    /// <param name="skipNoDll">如果为true，则跳过没有对应DLL文件的XML文件。默认为true。</param>
    /// <param name="savePath">保存已修补XML文件的路径。默认为 "zh-Hans"。</param>
    /// <param name="contentFilter">用于过滤内容的正则表达式。默认为 @"[\u4e00-\u9fa5]"，匹配所有中文字符。</param>
    /// <returns>一个表示异步操作的任务。</returns>
    public async Task Patch(
        CancellationToken cancellationToken,
        string[]? includeDirs = null,
        string[]? excludeFiles = null,
        bool skipNoDll = true,
        string savePath = "zh-Hans",
        string contentFilter = @"[\u4e00-\u9fa5]"
    )
    {
        includeDirs ??=
            _configuration.GetSection("IntelliSense:IncludeDirs").Get<string[]>()
            ?? throw new ArgumentNullException(nameof(includeDirs));
        excludeFiles ??=
            _configuration.GetSection("IntelliSense:ExcludeFiles").Get<string[]>() ?? [];
        _logger.LogInformation(
            """
            Patch IntelliSense Files:
                --includeDirs:  {includeDirs}
                --excludeFiles: {excludeFiles}
                --skipNoDll:    {skipNoDll}
                --savePath:     {savePath}
                --contentFilter:  {contentFilter}
            """,
            includeDirs,
            excludeFiles,
            skipNoDll,
            savePath,
            contentFilter
        );
        foreach (string dir in includeDirs)
        {
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
                if (File.Exists(saveFile))
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
                if (xmlFile.EndsWith(".bak.xml"))
                {
                    continue;
                }

                if (skipNoDll && !File.Exists(Path.ChangeExtension(xmlFile, "dll")))
                {
                    continue;
                }
                if (excludeFiles.Any(x => x.Equals(Path.GetFileName(xmlFile))))
                {
                    continue;
                }
                var file = IntelliSenseFile.Parse(xmlFile);
                if (file == null)
                {
                    continue;
                }

                _logger.LogInformation("Processing {xmlFile}", xmlFile);
                var hashes = file.GetContentsByTags(
                        ["summary", "param", "returns", "remarks", "typeparam"]
                    )
                    .Where(c => !c.IsNullOrWhiteSpace() && !c.IsRegexMatch(contentFilter))
                    .Select(c => c.ReplacExtraSpaces("").CalculateMd5())
                    .Distinct();
                var translations = await _dbContext
                    .Translations.Where(t => hashes.Contains(t.OriginalHash))
                    .ToDictionaryAsync(t => t.OriginalHash, t => t.Content, cancellationToken);
                if (translations.Count == 0)
                {
                    continue;
                }
                var allXmlElements = file.GetXmlElementsByTags(
                    ["summary", "param", "returns", "remarks", "typeparam"]
                );

                foreach (var xmlElement in allXmlElements)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    string key = xmlElement.InnerXml.ReplacExtraSpaces("").CalculateMd5();
                    if (!translations.ContainsKey(key))
                    {
                        continue;
                    }
                    string translation = translations[key];
                    xmlElement.InnerXml = translation;
                }
                file.SaveXml(saveFile);
            }
        }
    }
}
