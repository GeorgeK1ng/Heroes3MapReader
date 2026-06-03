using System.Globalization;
using System.Reflection;
using Heroes3MapReader.Logic.Models;
using NTextCat;

namespace Heroes3MapReader.Logic;

/// <summary>
/// Detects the most likely language of map descriptions using NTextCat language models.
/// </summary>
public static class MapLanguageDetector
{
    private const string UnknownLanguageCode = "und";

    private static readonly Lazy<RankedLanguageIdentifier?> LanguageIdentifier = new(LoadLanguageIdentifier);

    /// <summary>
    /// Detects the most likely language from a map description.
    /// </summary>
    /// <param name="description">The map description text.</param>
    /// <returns>The best-effort detected language.</returns>
    public static DetectedLanguage Detect(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return DetectedLanguage.Unknown;
        }

        RankedLanguageIdentifier? identifier = LanguageIdentifier.Value;
        if (identifier == null)
        {
            return DetectByScriptFallback(description);
        }

        var mostCertainLanguage = identifier
            .Identify(description)
            .FirstOrDefault();

        if (mostCertainLanguage == null)
        {
            return DetectByScriptFallback(description);
        }

        string languageCode = NormalizeLanguageCode(mostCertainLanguage.Item1.Iso639_3);
        if (languageCode == UnknownLanguageCode)
        {
            return DetectByScriptFallback(description);
        }

        return new DetectedLanguage(languageCode, GetLanguageName(languageCode));
    }

    private static RankedLanguageIdentifier? LoadLanguageIdentifier()
    {
        var factory = new RankedLanguageIdentifierFactory();

        foreach (string profilePath in GetLanguageModelPaths())
        {
            if (File.Exists(profilePath))
            {
                return factory.Load(profilePath);
            }
        }

        return null;
    }

    private static IEnumerable<string> GetLanguageModelPaths()
    {
        string? assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string[] baseDirectories = new[]
            {
                AppContext.BaseDirectory,
                assemblyDirectory,
            }
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();

        string[] modelFileNames =
        {
            "Wikipedia82.profile.xml",
            "Wiki82.profile.xml",
            "Core14.profile.xml",
        };

        foreach (string baseDirectory in baseDirectories)
        {
            foreach (string modelFileName in modelFileNames)
            {
                yield return Path.Combine(baseDirectory, "LanguageModels", modelFileName);
                yield return Path.Combine(baseDirectory, modelFileName);
            }
        }
    }

    private static DetectedLanguage DetectByScriptFallback(string description)
    {
        int cjkCount = 0;
        int cyrillicCount = 0;
        int letterCount = 0;

        foreach (char character in description)
        {
            if (char.IsLetter(character))
            {
                letterCount++;
            }

            if (IsCjk(character))
            {
                cjkCount++;
            }
            else if (IsCyrillic(character))
            {
                cyrillicCount++;
            }
        }

        if (letterCount == 0)
        {
            return DetectedLanguage.Unknown;
        }

        if (cjkCount >= 2 && cjkCount >= letterCount * 0.15)
        {
            return new DetectedLanguage("zho", "Chinese");
        }

        if (cyrillicCount >= 3 && cyrillicCount >= letterCount * 0.2)
        {
            return description.Any(IsUkrainianSpecificCyrillic)
                ? new DetectedLanguage("ukr", "Ukrainian")
                : new DetectedLanguage("rus", "Russian");
        }

        return DetectedLanguage.Unknown;
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return UnknownLanguageCode;
        }

        return languageCode.Trim().ToLowerInvariant() switch
        {
            "chi" => "zho",
            "cze" => "ces",
            "fre" => "fra",
            "ger" => "deu",
            "gre" => "ell",
            "per" => "fas",
            "rum" => "ron",
            "slo" => "slk",
            _ => languageCode.Trim().ToLowerInvariant(),
        };
    }

    private static string GetLanguageName(string languageCode)
    {
        string knownLanguageName = languageCode switch
        {
            "ces" => "Czech",
            "deu" => "German",
            "eng" => "English",
            "fra" => "French",
            "hun" => "Hungarian",
            "pol" => "Polish",
            "rus" => "Russian",
            "swe" => "Swedish",
            "ukr" => "Ukrainian",
            "zho" => "Chinese",
            UnknownLanguageCode => "Unknown",
            _ => string.Empty,
        };

        if (!string.IsNullOrEmpty(knownLanguageName))
        {
            return knownLanguageName;
        }

        CultureInfo? culture = CultureInfo
            .GetCultures(CultureTypes.NeutralCultures)
            .FirstOrDefault(culture => string.Equals(culture.ThreeLetterISOLanguageName, languageCode, StringComparison.OrdinalIgnoreCase));

        return culture?.EnglishName ?? languageCode;
    }

    private static bool IsCjk(char character)
    {
        return character is >= '\u3400' and <= '\u4DBF'
            or >= '\u4E00' and <= '\u9FFF'
            or >= '\uF900' and <= '\uFAFF';
    }

    private static bool IsCyrillic(char character)
    {
        return character is >= '\u0400' and <= '\u052F'
            or >= '\u2DE0' and <= '\u2DFF'
            or >= '\uA640' and <= '\uA69F';
    }

    private static bool IsUkrainianSpecificCyrillic(char character)
    {
        return "іІїЇєЄґҐ".Contains(character);
    }
}
