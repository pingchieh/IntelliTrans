using IntelliTrans.Core;
using IntelliTrans.Core.Extensions;
using IntelliTrans.Database;
using IntelliTrans.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntelliTrans.Cli.Commands;

internal partial class MainCommands
{
    /// <summary>
    /// 扫描IntelliSense文件，包括指定目录中的 XML 文件，并将其内容添加到数据库中。
    /// </summary>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <param name="skipNoDll">指示是否跳过缺少对应 DLL 文件的 XML 文件，默认为 true。</param>
    /// <param name="contentFilter">用于过滤内容的正则表达式，默认为过滤所有包含中文字符的内容。</param>
    /// <returns>一个表示异步加载操作的任务。</returns>
    public async Task Scan(
        CancellationToken cancellationToken,
        bool skipNoDll = true,
        string contentFilter = @"[\u4e00-\u9fa5]"
    )
    {
        string scanedListPath = "scaned.list";
        HashSet<string> scanedSet = [];
        if (File.Exists(scanedListPath))
        {
            scanedSet = [.. await File.ReadAllLinesAsync(scanedListPath)];
        }
        else
        {
            File.Create(scanedListPath).Dispose();
        }
        var excludeFiles =
            _configuration.GetSection("IntelliSense:ExcludeFiles").Get<string[]>() ?? [];
        List<string> includeDirs = [_configuration["IntelliSense:PacksDir"]!];
        var nugetDir = _configuration["IntelliSense:NugetDir"];
        if (!string.IsNullOrEmpty(nugetDir))
        {
            includeDirs.AddRange(
                _configuration
                    .GetSection("IntelliSense:IncludePackages")
                    .Get<string[]>()
                    ?.Select(d => Path.Combine(nugetDir, d))
                    .ToList()!
            );
        }
        _logger.LogInformation(
            """
            Scan IntelliSense Files:
                --skipNoDll:    {skipNoDll}
                --contentFilter:  {contentFilter}
                --includeDirs:  {includeDirs}
                --excludeFiles: {excludeFiles}
            """,
            skipNoDll,
            contentFilter,
            includeDirs,
            excludeFiles
        );

        foreach (string dir in includeDirs)
        {
            if (!Directory.Exists(dir))
            {
                _logger.LogWarning("目录不存在：{dir}", dir);
                continue;
            }
            string[] xmlFiles = Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories);
            foreach (string xmlFile in xmlFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                if (scanedSet.Contains(xmlFile))
                {
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
                _logger.LogInformation("Processing {xmlFile}", xmlFile);
                var file = IntelliSenseFile.Parse(xmlFile);
                if (file == null)
                {
                    continue;
                }
                if (file.IsTranslated())
                {
                    continue;
                }

                var allContents = file.GetContentsByTags([
                        "summary",
                        "param",
                        "returns",
                        "remarks",
                        "typeparam",
                        "exception",
                    ])
                    .Where(c => !c.IsNullOrWhiteSpace() && !c.IsRegexMatch(contentFilter));
                if (!allContents.Any())
                {
                    continue;
                }

                var contents = allContents
                    .Select(c => new IntelliSenseOriginal()
                    {
                        Content = c.Trim(),
                        Hash = c.ReplacExtraSpaces("").CalculateMd5(),
                    })
                    .DistinctBy(o => o.Hash)
                    .ToArray();
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IntelliSenseDbContext>();
                // 一次性查询所有已存在的哈希值
                var existingHashes = await dbContext
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
                    await dbContext.Originals.AddRangeAsync(newContents);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                if (scanedSet.Add(xmlFile))
                {
                    await File.AppendAllTextAsync(
                        scanedListPath,
                        xmlFile + Environment.NewLine,
                        cancellationToken
                    );
                }
            }
        }
    }
}
