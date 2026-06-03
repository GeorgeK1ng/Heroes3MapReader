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

    private static readonly Dictionary<string, DetectedLanguage> LatinFallbackLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["czech"] = new("ces", "Czech"),
        ["english"] = new("eng", "English"),
        ["polish"] = new("pol", "Polish"),
        ["german"] = new("deu", "German"),
        ["french"] = new("fra", "French"),
        ["hungarian"] = new("hun", "Hungarian"),
        ["swedish"] = new("swe", "Swedish"),
        ["spanish"] = new("spa", "Spanish"),
        ["italian"] = new("ita", "Italian"),
    };

    private static readonly Dictionary<string, HashSet<string>> LatinFallbackWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["czech"] = new(StringComparer.OrdinalIgnoreCase) { "aztekove", "byt", "byl", "byla", "den", "dva", "jejich", "jsou", "mapa", "mayove", "narody", "neprateli", "nezavislosti", "nyni", "ohrozovani", "pekelniky", "pro", "silami", "starymi", "vybojujte", "zamek" },
        ["english"] = new(StringComparer.OrdinalIgnoreCase) { "after", "against", "all", "and", "army", "battle", "castle", "defeat", "enemy", "find", "for", "hero", "king", "map", "must", "the", "this", "town", "war", "with", "you", "your" },
        ["polish"] = new(StringComparer.OrdinalIgnoreCase) { "armia", "bitwa", "bohater", "dla", "dwa", "jest", "kraina", "krol", "krolestwo", "mapa", "miasto", "musisz", "nie", "pokonaj", "przeciw", "skarbu", "twoj", "twoja", "wojna", "zamek", "znajdz" },
        ["german"] = new(StringComparer.OrdinalIgnoreCase) { "alle", "auf", "burg", "dein", "der", "die", "ein", "feind", "finde", "gegen", "held", "karte", "koenig", "konig", "land", "mit", "musst", "stadt", "und" },
        ["french"] = new(StringComparer.OrdinalIgnoreCase) { "avec", "carte", "chateau", "contre", "dans", "des", "ennemi", "heros", "les", "pour", "que", "quete", "roi", "royaume", "sur", "terre", "trouver", "une", "vous", "votre" },
        ["hungarian"] = new(StringComparer.OrdinalIgnoreCase) { "arany", "az", "csak", "ellenseg", "es", "feladat", "fold", "hos", "kell", "keresd", "kiraly", "kincs", "terkep", "var", "varos" },
        ["swedish"] = new(StringComparer.OrdinalIgnoreCase) { "alla", "borg", "den", "det", "din", "du", "efter", "fiende", "hitta", "hjalte", "karta", "kung", "land", "maste", "med", "mot", "och", "skatt", "stad" },
        ["spanish"] = new(StringComparer.OrdinalIgnoreCase) { "castillo", "contra", "debes", "derrota", "el", "enemigo", "encontrar", "guerra", "heroe", "mapa", "para", "reino", "tesoro", "tierra", "tu" },
        ["italian"] = new(StringComparer.OrdinalIgnoreCase) { "castello", "contro", "devi", "eroe", "guerra", "il", "mappa", "nemico", "per", "regno", "terra", "tesoro", "trova", "tuo" },
    };

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
            DetectedLanguage? latinLanguage = DetectLatinFallback(description);
            if (latinLanguage != null)
            {
                return latinLanguage;
            }
        }

        return DetectedLanguage.Unknown;
    }

    private static DetectedLanguage? DetectLatinFallback(string description)
    {
        string[] words = description
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeWord)
            .Where(word => word.Length > 1)
            .ToArray();

        Dictionary<string, int> scores = LatinFallbackWords.ToDictionary(
            pair => pair.Key,
            pair => words.Count(pair.Value.Contains),
            StringComparer.OrdinalIgnoreCase);

        foreach (char character in description)
        {
            if ("áčďéěíňóřšťúůýžÁČĎÉĚÍŇÓŘŠŤÚŮÝŽ".Contains(character))
            {
                scores["czech"] += 3;
            }
            else if ("ąćęłńóśźżĄĆĘŁŃÓŚŹŻ".Contains(character))
            {
                scores["polish"] += 3;
            }
            else if ("őűŐŰ".Contains(character))
            {
                scores["hungarian"] += 3;
            }
            else if ("åÅ".Contains(character))
            {
                scores["swedish"] += 3;
            }
            else if ("ßẞäöüÄÖÜ".Contains(character))
            {
                scores["german"] += 2;
                scores["swedish"] += 1;
            }
            else if ("àâæçèêëîïôœùûüÿÀÂÆÇÈÊËÎÏÔŒÙÛÜŸ".Contains(character))
            {
                scores["french"] += 2;
            }
        }

        KeyValuePair<string, int> bestScore = scores.MaxBy(pair => pair.Value);
        return bestScore.Value >= 2 ? LatinFallbackLanguages[bestScore.Key] : null;
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
        return new string(word.Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray());
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
