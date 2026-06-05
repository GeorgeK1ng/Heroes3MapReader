using System.Globalization;
using System.Reflection;
using System.Text;
using Heroes3MapReader.Logic.Models;
using NTextCat;

namespace Heroes3MapReader.Logic;

/// <summary>
/// Detects the most likely language of map descriptions using NTextCat models and optional public dictionary fallbacks.
/// </summary>
public static class MapLanguageDetector
{
    private const string UnknownLanguageCode = "und";

    private static readonly IReadOnlyDictionary<string, DetectedLanguage> PublicDictionaryLanguages = new Dictionary<string, DetectedLanguage>(StringComparer.OrdinalIgnoreCase)
    {
        ["ces"] = new("ces", "Czech"),
        ["cze"] = new("ces", "Czech"),
        ["cs"] = new("ces", "Czech"),
        ["cs_cz"] = new("ces", "Czech"),
        ["eng"] = new("eng", "English"),
        ["en"] = new("eng", "English"),
        ["en_us"] = new("eng", "English"),
        ["en_gb"] = new("eng", "English"),
        ["pol"] = new("pol", "Polish"),
        ["pl"] = new("pol", "Polish"),
        ["pl_pl"] = new("pol", "Polish"),
        ["deu"] = new("deu", "German"),
        ["ger"] = new("deu", "German"),
        ["de"] = new("deu", "German"),
        ["de_de"] = new("deu", "German"),
        ["fra"] = new("fra", "French"),
        ["fre"] = new("fra", "French"),
        ["fr"] = new("fra", "French"),
        ["fr_fr"] = new("fra", "French"),
        ["hun"] = new("hun", "Hungarian"),
        ["hu"] = new("hun", "Hungarian"),
        ["hu_hu"] = new("hun", "Hungarian"),
        ["swe"] = new("swe", "Swedish"),
        ["sv"] = new("swe", "Swedish"),
        ["sv_se"] = new("swe", "Swedish"),
        ["spa"] = new("spa", "Spanish"),
        ["es"] = new("spa", "Spanish"),
        ["es_es"] = new("spa", "Spanish"),
        ["ita"] = new("ita", "Italian"),
        ["it"] = new("ita", "Italian"),
        ["it_it"] = new("ita", "Italian"),
    };

    private static readonly Lazy<RankedLanguageIdentifier?> LanguageIdentifier = new(LoadLanguageIdentifier);
    private static readonly Lazy<IReadOnlyDictionary<DetectedLanguage, HashSet<string>>> PublicWordDictionaries = new(LoadPublicWordDictionaries);

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

        DetectedLanguage? languageSpecificCharacterFallback = DetectByLanguageSpecificCharacters(description);
        if (languageSpecificCharacterFallback != null)
        {
            return languageSpecificCharacterFallback;
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
        if (languageCode == UnknownLanguageCode
            || (IsCyrillicLanguageCode(languageCode) && !HasCyrillicText(description))
            || (!IsCjkLanguageCode(languageCode) && HasCjkText(description)))
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
        int latinCount = 0;
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
            else if (IsBasicLatinOrLatinExtended(character))
            {
                latinCount++;
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

        if (latinCount >= letterCount * 0.6)
        {
            DetectedLanguage? latinLanguage = DetectByPublicDictionary(description);
            if (latinLanguage != null)
            {
                return latinLanguage;
            }
        }

        return DetectedLanguage.Unknown;
    }

    private static DetectedLanguage? DetectByLanguageSpecificCharacters(string description)
    {
        Dictionary<DetectedLanguage, int> scores = new()
        {
            [new DetectedLanguage("ces", "Czech")] = CountAny(description, "čďěňřšťůžČĎĚŇŘŠŤŮŽ"),
            [new DetectedLanguage("pol", "Polish")] = CountAny(description, "ąćęłńśźżĄĆĘŁŃŚŹŻ"),
            [new DetectedLanguage("hun", "Hungarian")] = CountAny(description, "őűŐŰ"),
            [new DetectedLanguage("deu", "German")] = CountAny(description, "ßẞ"),
            [new DetectedLanguage("swe", "Swedish")] = CountAny(description, "åÅ"),
            [new DetectedLanguage("fra", "French")] = CountAny(description, "æçœÆÇŒ"),
            [new DetectedLanguage("spa", "Spanish")] = CountAny(description, "ñÑ¿¡"),
        };

        KeyValuePair<DetectedLanguage, int> bestScore = scores.MaxBy(pair => pair.Value);
        int secondBestScore = scores
            .Where(pair => pair.Key != bestScore.Key)
            .Max(pair => pair.Value);

        return bestScore.Value >= 1 && bestScore.Value >= secondBestScore + 1
            ? bestScore.Key
            : null;
    }

    private static DetectedLanguage? DetectByPublicDictionary(string description)
    {
        IReadOnlyDictionary<DetectedLanguage, HashSet<string>> dictionaries = PublicWordDictionaries.Value;
        if (dictionaries.Count == 0)
        {
            return null;
        }

        string[] words = description
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeWord)
            .Where(word => word.Length > 1)
            .ToArray();

        if (words.Length == 0)
        {
            return null;
        }

        KeyValuePair<DetectedLanguage, int>[] scores = dictionaries
            .Select(dictionary => new KeyValuePair<DetectedLanguage, int>(
                dictionary.Key,
                words.Count(dictionary.Value.Contains)))
            .OrderByDescending(pair => pair.Value)
            .ToArray();

        KeyValuePair<DetectedLanguage, int> bestScore = scores[0];
        int secondBestScore = scores.Length > 1 ? scores[1].Value : 0;

        return bestScore.Value >= 2 && bestScore.Value >= secondBestScore + 2
            ? bestScore.Key
            : null;
    }

    private static IReadOnlyDictionary<DetectedLanguage, HashSet<string>> LoadPublicWordDictionaries()
    {
        Dictionary<DetectedLanguage, HashSet<string>> dictionaries = new();

        foreach (string dictionaryPath in GetPublicDictionaryPaths())
        {
            string dictionaryKey = NormalizeDictionaryKey(Path.GetFileNameWithoutExtension(dictionaryPath));
            if (!PublicDictionaryLanguages.TryGetValue(dictionaryKey, out DetectedLanguage? language))
            {
                continue;
            }

            HashSet<string> words = LoadDictionaryWords(dictionaryPath);
            if (words.Count > 0)
            {
                dictionaries[language] = words;
            }
        }

        return dictionaries;
    }

    private static IEnumerable<string> GetPublicDictionaryPaths()
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

        foreach (string baseDirectory in baseDirectories)
        {
            string dictionariesDirectory = Path.Combine(baseDirectory, "LanguageDictionaries");
            if (!Directory.Exists(dictionariesDirectory))
            {
                continue;
            }

            foreach (string dictionaryPath in Directory.EnumerateFiles(dictionariesDirectory, "*.dic"))
            {
                yield return dictionaryPath;
            }

            foreach (string dictionaryPath in Directory.EnumerateFiles(dictionariesDirectory, "*.txt"))
            {
                yield return dictionaryPath;
            }
        }
    }

    private static HashSet<string> LoadDictionaryWords(string dictionaryPath)
    {
        HashSet<string> words = new(StringComparer.OrdinalIgnoreCase);

        foreach (string rawLine in File.ReadLines(dictionaryPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('/'))
            {
                continue;
            }

            if (words.Count == 0 && line.All(char.IsDigit))
            {
                continue;
            }

            string dictionaryWord = line.Split('/', 2, StringSplitOptions.TrimEntries)[0];
            string normalizedWord = NormalizeWord(dictionaryWord);
            if (normalizedWord.Length > 1)
            {
                words.Add(normalizedWord);
            }
        }

        return words;
    }

    private static string NormalizeDictionaryKey(string dictionaryKey)
    {
        return dictionaryKey
            .Trim()
            .Replace('-', '_')
            .ToLowerInvariant();
    }

    private static int CountAny(string text, string characters)
    {
        return text.Count(characters.Contains);
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
            "spa" => "Spanish",
            "ita" => "Italian",
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

    private static bool IsCyrillicLanguageCode(string languageCode)
    {
        return languageCode is "rus" or "ukr" or "bel" or "bul" or "mkd" or "srp";
    }

    private static bool IsCjkLanguageCode(string languageCode)
    {
        return languageCode is "zho" or "jpn" or "kor";
    }

    private static bool HasCyrillicText(string text)
    {
        return text.Count(IsCyrillic) >= 3;
    }

    private static bool HasCjkText(string text)
    {
        return text.Count(IsCjk) >= 2;
    }

    private static bool IsBasicLatinOrLatinExtended(char character)
    {
        return character is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '\u00C0' and <= '\u024F';
    }

    private static string NormalizeWord(string word)
    {
        string lettersOnly = new(word.Where(char.IsLetter).ToArray());
        string normalized = lettersOnly.Normalize(NormalizationForm.FormD);

        return new string(normalized
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .Select(char.ToLowerInvariant)
            .ToArray());
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
