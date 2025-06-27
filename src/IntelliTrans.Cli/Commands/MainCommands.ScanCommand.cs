using IntelliTrans.Core;
using IntelliTrans.Core.Extensions;
using IntelliTrans.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IntelliTrans.Cli.Commands;

internal partial class MainCommands
{
    /// <summary>
    /// 扫描IntelliSense文件，包括指定目录中的 XML 文件，并将其内容添加到数据库中。
    /// </summary>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <param name="includeDirs">包含 XML 文件的目录数组，默认为从配置中读取的 "IntelliSense:IncludeDirs" 节。</param>
    /// <param name="excludeFiles">要排除的 XML 文件名数组，默认为从配置中读取的 "IntelliSense:ExcludeFiles" 节。</param>
    /// <param name="skipNoDll">指示是否跳过缺少对应 DLL 文件的 XML 文件，默认为 true。</param>
    /// <param name="contentFilter">用于过滤内容的正则表达式，默认为过滤所有包含中文字符的内容。</param>
    /// <returns>一个表示异步加载操作的任务。</returns>
    public async Task Scan(
        CancellationToken cancellationToken,
        string[]? includeDirs = null,
        string[]? excludeFiles = null,
        bool skipNoDll = true,
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
            Scan IntelliSense Files:
                --includeDirs:  {includeDirs}
                --excludeFiles: {excludeFiles}
                --skipNoDll:    {skipNoDll}
                --contentFilter:  {contentFilter}
            """,
            includeDirs,
            excludeFiles,
            skipNoDll,
            contentFilter
        );

        await _dbContext.Database.MigrateAsync();

        foreach (string dir in includeDirs)
        {
            string[] xmlFiles = Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories);
            foreach (string xmlFile in xmlFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
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
                var allContents = file.GetContentsByTags(
                        ["summary", "param", "returns", "remarks", "typeparam"]
                    )
                    .Where(c => !c.IsNullOrWhiteSpace() && !c.IsRegexMatch(contentFilter));

                var contents = allContents
                    .Select(c => new IntelliSenseOriginal()
                    {
                        Content = c.Trim(),
                        Hash = c.ReplacExtraSpaces("").CalculateMd5(),
                    })
                    .DistinctBy(o => o.Hash)
                    .ToArray();

                // 一次性查询所有已存在的哈希值
                var existingHashes = await _dbContext
                    .Originals.Where(o => contents.Select(c => c.Hash).Contains(o.Hash))
                    .Select(o => o.Hash)
                    .ToListAsync(cancellationToken);

                // 只添加不存在的记录
                var newContents = contents.Where(c => !existingHashes.Contains(c.Hash)).ToArray();

                if (newContents.Length != 0)
                {
                    _logger.LogInformation(
                        "Found {newContentsCount} new contents in {xmlFile}",
                        newContents.Length,
                        xmlFile
                    );
                    await _dbContext.Originals.AddRangeAsync(newContents);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }
}
