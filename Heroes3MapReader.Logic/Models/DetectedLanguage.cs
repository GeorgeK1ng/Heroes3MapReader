namespace Heroes3MapReader.Logic.Models;

/// <summary>
/// Represents a detected natural language.
/// </summary>
public sealed record DetectedLanguage(string Code, string Name)
{
    /// <summary>
    /// Language detection was not able to identify the text.
    /// </summary>
    public static DetectedLanguage Unknown { get; } = new("und", "Unknown");
}
