using Microsoft.Extensions.Configuration;

namespace IntelliTrans.Cli.Commands;

internal partial class MainCommands
{
    private static readonly string[] XmlDocTags =
    [
        "summary",
        "param",
        "returns",
        "remarks",
        "typeparam",
        "exception",
    ];

    private FileOptions CreateFileOptions(bool skipNoDll, string contentFilter)
    {
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
                    ?? []
            );
        }

        return new FileOptions(includeDirs, excludeFiles, skipNoDll, contentFilter);
    }

    private static bool ShouldSkipXmlFile(string xmlFile, FileOptions options)
    {
        if (xmlFile.EndsWith(".bak.xml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (options.SkipNoDll && !File.Exists(Path.ChangeExtension(xmlFile, "dll")))
        {
            return true;
        }

        return options.ExcludeFiles.Any(x => x.Equals(Path.GetFileName(xmlFile)));
    }

    private sealed record FileOptions(
        IReadOnlyList<string> IncludeDirs,
        IReadOnlyList<string> ExcludeFiles,
        bool SkipNoDll,
        string ContentFilter
    );
}
