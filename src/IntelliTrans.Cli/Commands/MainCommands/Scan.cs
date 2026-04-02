using IntelliTrans.Core;
using IntelliTrans.Core.Extensions;
using IntelliTrans.Database;
using IntelliTrans.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntelliTrans.Cli.Commands;

internal partial class MainCommands
{
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
            scanedSet = [.. await File.ReadAllLinesAsync(scanedListPath, cancellationToken)];
        }
        else
        {
            File.Create(scanedListPath).Dispose();
        }

        var options = CreateFileOptions(skipNoDll, contentFilter);
        _logger.LogInformation(
            """
            Scan IntelliSense Files:
                --skipNoDll:    {skipNoDll}
                --contentFilter:  {contentFilter}
                --includeDirs:  {includeDirs}
                --excludeFiles: {excludeFiles}
            """,
            options.SkipNoDll,
            options.ContentFilter,
            options.IncludeDirs,
            options.ExcludeFiles
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
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (scanedSet.Contains(xmlFile) || ShouldSkipXmlFile(xmlFile, options))
                {
                    continue;
                }

                _logger.LogInformation("Processing {xmlFile}", xmlFile);
                var file = IntelliSenseFile.Parse(xmlFile);
                if (file == null || file.IsTranslated())
                {
                    continue;
                }

                var allContents = file.GetContentsByTags(XmlDocTags)
                    .Where(c => !c.IsNullOrWhiteSpace() && !c.IsRegexMatch(options.ContentFilter));
                if (!allContents.Any())
                {
                    continue;
                }

                var contents = allContents
                    .Select(c => new IntelliSenseOriginal
                    {
                        Content = c.Trim(),
                        Hash = c.ReplacExtraSpaces("").CalculateMd5(),
                    })
                    .DistinctBy(o => o.Hash)
                    .ToArray();

                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IntelliSenseDbContext>();
                var existingHashes = await dbContext
                    .Originals.Where(o => contents.Select(c => c.Hash).Contains(o.Hash))
                    .Select(o => o.Hash)
                    .ToListAsync(cancellationToken);

                var newContents = contents.Where(c => !existingHashes.Contains(c.Hash)).ToArray();
                if (newContents.Length != 0)
                {
                    _logger.LogInformation(
                        "Found {newContentsCount} new contents in {xmlFile}",
                        newContents.Length,
                        xmlFile
                    );
                    await dbContext.Originals.AddRangeAsync(newContents, cancellationToken);
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
