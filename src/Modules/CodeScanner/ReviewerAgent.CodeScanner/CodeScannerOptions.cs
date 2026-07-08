namespace ReviewerAgent.CodeScanner;

/// <summary>Code-scanning behaviour and limits. Bound from the "CodeScanner" section.</summary>
public sealed class CodeScannerOptions
{
    public const string SectionName = "CodeScanner";

    /// <summary>File extensions/suffixes to skip entirely (e.g. ".json", ".md", ".csproj").</summary>
    public string[] IgnoredFilePatterns { get; set; } = [];

    /// <summary>Maximum number of changed files analyzed per run.</summary>
    public int MaxFilesToAnalyze { get; set; } = 50;

    /// <summary>Maximum characters of XML documentation retained per type.</summary>
    public int MaxContextSizeChars { get; set; } = 4000;

    /// <summary>Maximum number of method signatures retained per file.</summary>
    public int MaxMethodsPerFile { get; set; } = 60;
}
