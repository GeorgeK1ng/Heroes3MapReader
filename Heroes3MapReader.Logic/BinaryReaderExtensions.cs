using System.Text;

namespace Heroes3MapReader.Logic;

/// <summary>
/// Static utility class for reading data from BinaryReader with H3M map format-specific logic.
/// </summary>
internal static class BinaryReaderExtensions
{
    public sealed record DecodedString(string Text, Encoding Encoding, int Score, byte[] Bytes);

    static BinaryReaderExtensions()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Reads a string from the binary reader using the H3M format.
    /// Format: 4-byte length prefix followed by the string bytes.
    /// </summary>
    /// <param name="reader">The binary reader to read from.</param>
    /// <param name="encoding">The preferred encoding to use for decoding the string.</param>
    /// <returns>The decoded string.</returns>
    /// <exception cref="InvalidDataException">Thrown when the string length is too large (over 100,000 bytes).</exception>
    public static string ReadString(BinaryReader reader, Encoding encoding)
    {
        uint length = reader.ReadUInt32();
        if (length == 0)
        {
            return string.Empty;
        }

        if (length > 100000)
        {
            throw new InvalidDataException($"String length too large: {length}");
        }

        return ReadStringWithDetectedEncoding(reader, encoding, length).Text;
    }

    public static DecodedString ReadStringWithDetectedEncoding(BinaryReader reader, Encoding encoding)
    {
        uint length = reader.ReadUInt32();
        return ReadStringWithDetectedEncoding(reader, encoding, length);
    }

    private static DecodedString ReadStringWithDetectedEncoding(BinaryReader reader, Encoding encoding, uint length)
    {
        if (length == 0)
        {
            return new DecodedString(string.Empty, encoding, 0, []);
        }

        if (length > 100000)
        {
            throw new InvalidDataException($"String length too large: {length}");
        }

        byte[] bytes = reader.ReadBytes((int)length);
        return DetectString(bytes, encoding);
    }

    public static DecodedString DecodeString(byte[] bytes, Encoding encoding)
    {
        string text = encoding.GetString(bytes);
        return new DecodedString(text, encoding, ScoreDecodedText(text), bytes);
    }

    private static DecodedString DetectString(byte[] bytes, Encoding preferredEncoding)
    {
        return GetCandidateEncodings(preferredEncoding)
            .Select(encoding => TryDecode(bytes, encoding))
            .Where(candidate => candidate != null)
            .OrderByDescending(candidate => candidate!.Score)
            .First()!;
    }

    private static DecodedString? TryDecode(byte[] bytes, Encoding encoding)
    {
        try
        {
            string text = encoding.GetString(bytes);
            return new DecodedString(text, encoding, ScoreDecodedText(text), bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static IEnumerable<Encoding> GetCandidateEncodings(Encoding preferredEncoding)
    {
        Encoding? utf8 = GetStrictUtf8Encoding();
        if (utf8 != null)
        {
            yield return utf8;
        }

        int[] codePages =
        {
            936,   // Simplified Chinese (GBK)
            54936, // Simplified Chinese (GB18030)
            950,   // Traditional Chinese (Big5)
            1250,  // Central European: Czech, Polish, Hungarian, etc.
            1251,  // Cyrillic: Russian, Ukrainian, etc.
            1252,  // Western European: English, German, French, Swedish, etc.
            1253,  // Greek
            1254,  // Turkish
            1257,  // Baltic languages
            1258,  // Vietnamese
        };

        foreach (int codePage in codePages)
        {
            yield return Encoding.GetEncoding(codePage);
        }

        yield return preferredEncoding;
    }

    private static Encoding? GetStrictUtf8Encoding()
    {
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    }

    private static int ScoreDecodedText(string text)
    {
        int score = 0;
        int latinCount = 0;
        int latinExtendedCount = 0;
        int cyrillicCount = 0;
        int cjkCount = 0;
        int otherLetterCount = 0;
        int replacementCount = 0;
        int controlCount = 0;

        foreach (char character in text)
        {
            if (character == '\uFFFD')
            {
                replacementCount++;
                continue;
            }

            if (char.IsControl(character) && !char.IsWhiteSpace(character))
            {
                controlCount++;
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                score += 2;
                continue;
            }

            if (char.IsPunctuation(character) || char.IsDigit(character) || char.IsSymbol(character))
            {
                score += 1;
                continue;
            }

            if (!char.IsLetter(character))
            {
                continue;
            }

            score += 2;

            if (IsBasicLatin(character))
            {
                latinCount++;
            }
            else if (IsLatinExtended(character))
            {
                latinCount++;
                latinExtendedCount++;
                score += 6;
            }
            else if (IsCyrillic(character))
            {
                cyrillicCount++;
            }
            else if (IsCjk(character))
            {
                cjkCount++;
                score += 4;
            }
            else
            {
                otherLetterCount++;
            }
        }

        score -= replacementCount * 1000;
        score -= controlCount * 100;

        int letterCount = latinCount + cyrillicCount + cjkCount + otherLetterCount;

        if (cjkCount > 0)
        {
            score += cjkCount * 40;
        }

        if (cjkCount >= 2 && cjkCount >= letterCount * 0.2)
        {
            score += 1000 + cjkCount * 80;
        }

        if (latinExtendedCount > 0)
        {
            score += CountCharacters(text, "áčďéěíňóřšťúůýžÁČĎÉĚÍŇÓŘŠŤÚŮÝŽ") * 15;
            score += CountCharacters(text, "ąćęłńóśźżĄĆĘŁŃÓŚŹŻ") * 15;
            score += CountCharacters(text, "őűŐŰ") * 15;
            score += CountCharacters(text, "àâæçèêëîïôœùûüÿÀÂÆÇÈÊËÎÏÔŒÙÛÜŸ") * 10;
            score += CountCharacters(text, "äöüßÄÖÜẞåÅ") * 10;
        }

        if (latinExtendedCount > Math.Max(4, latinCount * 0.35) && CountAsciiLetters(text) < latinExtendedCount)
        {
            score -= latinExtendedCount * 20;
        }

        if (latinCount > cyrillicCount * 2 && cyrillicCount > 0)
        {
            score -= cyrillicCount * 30;
        }

        if (cyrillicCount > latinCount * 2 && latinCount > 0)
        {
            score -= latinCount * 10;
        }

        if (cjkCount > 0 && latinCount + cyrillicCount + otherLetterCount > cjkCount * 3)
        {
            score -= cjkCount * 20;
        }

        return score;
    }

    private static int CountAsciiLetters(string text)
    {
        return text.Count(IsBasicLatin);
    }

    private static bool IsBasicLatin(char character)
    {
        return character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private static bool IsLatinExtended(char character)
    {
        return character is >= '\u00C0' and <= '\u024F';
    }

    private static bool IsCyrillic(char character)
    {
        return character is >= '\u0400' and <= '\u052F'
            or >= '\u2DE0' and <= '\u2DFF'
            or >= '\uA640' and <= '\uA69F';
    }

    private static bool IsCjk(char character)
    {
        return character is >= '\u3400' and <= '\u4DBF'
            or >= '\u4E00' and <= '\u9FFF'
            or >= '\uF900' and <= '\uFAFF';
    }

    private static int CountCharacters(string text, string characters)
    {
        return text.Count(characters.Contains);
    }

    /// <summary>
    /// Reads a variable number of bytes as a uint32 value.
    /// </summary>
    /// <param name="reader">The binary reader to read from.</param>
    /// <param name="bytes">Number of bytes to read (1, 2, or 4).</param>
    /// <returns>The value read as a uint32.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when bytes is not 1, 2, or 4.</exception>
    public static uint ReadBytesAsUint32(BinaryReader reader, uint bytes)
    {
        if (bytes == 1)
        {
            return reader.ReadByte();
        }

        if (bytes == 2)
        {
            return reader.ReadUInt16();
        }

        if (bytes == 4)
        {
            return reader.ReadUInt32();
        }

        throw new ArgumentOutOfRangeException($"Unsupported byte read count: {bytes}");
    }

    /// <summary>
    /// Clamps a value between a minimum and maximum value.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    /// <returns>The clamped value.</returns>
    public static uint Clamp(uint value, uint min, uint max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
