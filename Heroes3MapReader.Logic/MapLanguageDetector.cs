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

    private static readonly HashSet<string> GermanWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "alle", "auf", "burg", "dein", "deine", "dem", "den", "der", "des", "die", "dies", "diese", "dorf", "ein", "eine",
        "feind", "feinde", "finde", "gegen", "gold", "held", "karte", "koenig", "konig", "koenigreich", "konigreich", "land",
        "mit", "musst", "nach", "nur", "reich", "schatz", "stadt", "und", "von", "vor", "wird", "zu",
    };

    private static readonly HashSet<string> FrenchWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "apres", "avec", "avant", "carte", "chateau", "contre", "dans", "des", "doit", "ennemi", "ennemis", "est", "etre",
        "heros", "les", "monde", "or", "pour", "que", "quete", "roi", "royaume", "sur", "terre", "tes", "toi", "ton", "tresor", "trouver", "une", "vous", "votre",
    };

    private static readonly HashSet<string> UkrainianWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "але", "битва", "вiйна", "війна", "вiн", "він", "вона", "вони", "ворог", "вороги", "герой", "герої", "знайди", "замок", "земля",
        "карта", "король", "королiвство", "королівство", "мiсто", "місто", "повинен", "проти", "скарб", "твiй", "твій", "твоя", "твоє", "треба", "усi", "усі", "царство",
    };

    private static readonly HashSet<string> RussianWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "битва", "враг", "враги", "все", "герой", "герои", "город", "должен", "замок", "земля", "карта", "король",
        "королевство", "найди", "против", "сокровище", "твой", "твоя", "твое", "царь", "царство",
    };

    private static readonly HashSet<string> HungarianWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "arany", "az", "csak", "ellenseg", "ellen", "es", "feladat", "fold", "hos", "hosok", "kell", "keresd", "kiraly", "kiralysag",
        "kincs", "kuzdelem", "meg", "minden", "terkep", "var", "varos", "vagy", "vilag",
    };

    private static readonly HashSet<string> SwedishWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "alla", "armé", "arme", "borg", "den", "det", "din", "dina", "dit", "du", "efter", "fiende", "fiender", "före", "fore", "guld",
        "hitta", "hjälte", "hjalte", "karta", "kung", "kungarike", "land", "måste", "maste", "med", "mot", "och", "skatt", "stad", "strid",
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
            return MapLanguage.Unknown;
        }

        if (cjkCount >= 2 && cjkCount >= letterCount * 0.15)
        {
            return MapLanguage.Chinese;
        }

        string[] words = description
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeWord)
            .Where(word => word.Length > 1)
            .ToArray();

        if (cyrillicCount >= 3 && cyrillicCount >= letterCount * 0.2)
        {
            return DetectCyrillicLanguage(description, words);
        }

        if (latinCount == 0)
        {
            return MapLanguage.Other;
        }

        Dictionary<MapLanguage, int> scores = GetLatinLanguageScores(description, words);
        KeyValuePair<MapLanguage, int> bestScore = scores.MaxBy(score => score.Value);

        if (bestScore.Value >= 2)
        {
            return bestScore.Key;
        }

        return MapLanguage.Other;
    }

    private static MapLanguage DetectCyrillicLanguage(string description, string[] words)
    {
        int ukrainianScore = words.Count(UkrainianWords.Contains);
        int russianScore = words.Count(RussianWords.Contains);

        if (description.Any(IsUkrainianSpecificCyrillic))
        {
            ukrainianScore += 2;
        }

        if (ukrainianScore >= 2 && ukrainianScore >= russianScore)
        {
            return MapLanguage.Ukrainian;
        }

        return MapLanguage.Russian;
    }

    private static Dictionary<MapLanguage, int> GetLatinLanguageScores(string description, string[] words)
    {
        var scores = new Dictionary<MapLanguage, int>
        {
            [MapLanguage.Czech] = words.Count(CzechWords.Contains),
            [MapLanguage.English] = words.Count(EnglishWords.Contains),
            [MapLanguage.German] = words.Count(GermanWords.Contains),
            [MapLanguage.French] = words.Count(FrenchWords.Contains),
            [MapLanguage.Hungarian] = words.Count(HungarianWords.Contains),
            [MapLanguage.Swedish] = words.Count(SwedishWords.Contains),
        };

        foreach (char character in description)
        {
            if (IsCzechSpecificLatin(character))
            {
                scores[MapLanguage.Czech] += 2;
            }
            else if (IsGermanSpecificLatin(character))
            {
                scores[MapLanguage.German] += 2;
            }
            else if (IsFrenchSpecificLatin(character))
            {
                scores[MapLanguage.French] += 2;
            }
            else if (IsHungarianSpecificLatin(character))
            {
                scores[MapLanguage.Hungarian] += 2;
            }
            else if (IsSwedishSpecificLatin(character))
            {
                scores[MapLanguage.Swedish] += 2;
            }
        }

        return scores;
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

    private static bool IsUkrainianSpecificCyrillic(char character)
    {
        return "іІїЇєЄґҐ".Contains(character);
    }

    private static bool IsCzechSpecificLatin(char character)
    {
        return "čďěňřšťůžČĎĚŇŘŠŤŮŽ".Contains(character);
    }

    private static bool IsGermanSpecificLatin(char character)
    {
        return "ßẞ".Contains(character);
    }

    private static bool IsFrenchSpecificLatin(char character)
    {
        return "àâæçèêëîïôœùûüÿÀÂÆÇÈÊËÎÏÔŒÙÛÜŸ".Contains(character);
    }

    private static bool IsHungarianSpecificLatin(char character)
    {
        return "őűŐŰ".Contains(character);
    }

    private static bool IsSwedishSpecificLatin(char character)
    {
        return "åÅ".Contains(character);
    }

    private static string NormalizeWord(string word)
    {
        return new string(word.Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray());
    }
}
