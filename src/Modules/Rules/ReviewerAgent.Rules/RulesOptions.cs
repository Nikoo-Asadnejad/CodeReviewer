namespace ReviewerAgent.Rules;

/// <summary>Rule engine settings. Bound from the "Rules" section.</summary>
public sealed class RulesOptions
{
    public const string SectionName = "Rules";

    /// <summary>Folder scanned for *.md rule documents.</summary>
    public string Folder { get; set; } = "Rules";
}
