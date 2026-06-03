using Heroes3MapReader.Logic.Models.Enums;

namespace Heroes3MapReader.Logic;

/// <summary>
/// Detects the most likely language of map descriptions using lightweight script and keyword heuristics.
/// </summary>
public static class MapLanguageDetector
{
    private static readonly HashSet<string> CzechWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ahoj", "bez", "bude", "budou", "byt", "byl", "byla", "byli", "cesta", "ceska", "ceske", "cesky",
        "dal", "dalsi", "den", "dobyt", "hrad", "hradu", "hradem", "hrady", "hraje", "hrac", "hraci", "hracu",
        "jednou", "jses", "jsi", "jsou", "kral", "krale", "kralovstvi", "ktery", "ktera", "ktere", "mapa", "mesto", "mesta",
        "musis", "muzete", "najdi", "najit", "nepritel", "nepritele", "nepratelske", "ostrov", "poklad", "poraz", "porazit",
        "pro", "proti", "pred", "pres", "pribeh", "sever", "spojenec", "tvoje", "tvoji", "tvuj", "vase", "vasich", "vyhrat",
        "vitezstvi", "vojsku", "zachran", "zeme", "ziskej", "ziskat", "zlo", "znovu", "zustal", "uzemi", "ukol", "utok", "zamek", "zamku",
    };

    private static readonly HashSet<string> EnglishWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "after", "against", "all", "allies", "army", "battle", "before", "castle", "defeat", "enemy", "find", "for", "gold",
        "hero", "king", "kingdom", "land", "lands", "lord", "map", "must", "only", "player", "players", "quest", "the", "this", "town", "treasure",
        "two", "victory", "war", "will", "with", "you", "your",
    };

    /// <summary>
    /// Detects the most likely language from a map description.
    /// </summary>
    /// <param name="description">The map description text.</param>
    /// <returns>The best-effort detected language.</returns>
    public static MapLanguage Detect(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return MapLanguage.Unknown;
        }

        int cjkCount = 0;
        int cyrillicCount = 0;
        int latinCount = 0;
        int czechDiacriticsCount = 0;
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

            if (IsCzechDiacritic(character))
            {
                czechDiacriticsCount++;
            }
        }

        if (letterCount == 0)
        {
            return MapLanguage.Unknown;
        }

        if (cjkCount >= 2 && cjkCount >= letterCount * 0.15)
        {
            return MapLanguage.Chinese;
        }

        if (cyrillicCount >= 3 && cyrillicCount >= letterCount * 0.2)
        {
            return MapLanguage.Russian;
        }

        if (latinCount == 0)
        {
            return MapLanguage.Other;
        }

        if (czechDiacriticsCount > 0)
        {
            return MapLanguage.Czech;
        }

        string[] words = description
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeWord)
            .Where(word => word.Length > 1)
            .ToArray();

        int czechScore = words.Count(CzechWords.Contains);
        int englishScore = words.Count(EnglishWords.Contains);

        if (czechScore >= 2 && czechScore >= englishScore)
        {
            return MapLanguage.Czech;
        }

        if (englishScore >= 2)
        {
            return MapLanguage.English;
        }

        return MapLanguage.Other;
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

    private static bool IsBasicLatinOrLatinExtended(char character)
    {
        return character is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '\u00C0' and <= '\u024F';
    }

    private static bool IsCzechDiacritic(char character)
    {
        return "áčďéěíňóřšťúůýžÁČĎÉĚÍŇÓŘŠŤÚŮÝŽ".Contains(character);
    }

    private static string NormalizeWord(string word)
    {
        return new string(word.Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray());
    }
}
