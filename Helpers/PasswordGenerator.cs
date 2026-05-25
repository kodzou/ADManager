using System.Security.Cryptography;
using System.Text;

namespace ADManager.Helpers;

public static class PasswordGenerator
{
    // Аналог New-MemoPassword
    public static string GenerateMemorable(int length = 14)
    {
        var consonants = "bcdfghjklmnpqrstvwxyz".ToCharArray();
        var vowels     = "aeiou".ToCharArray();
        var digits     = "0123456789".ToCharArray();

        var result    = new StringBuilder();
        int syllablePos = 0;
        string blockType = "syllable";
        int blockLen    = 0;

        while (result.Length < length)
        {
            if (blockType == "syllable")
            {
                if (blockLen >= 10) { blockType = "digit"; blockLen = 0; continue; }
                char ch = syllablePos % 2 == 0
                    ? GetRandom(consonants)
                    : GetRandom(vowels);
                if (blockLen == 0 || blockLen == 5)
                    ch = char.ToUpper(ch);
                result.Append(ch);
                syllablePos++;
                blockLen++;
            }
            else
            {
                if (blockLen >= 3) { blockType = "syllable"; syllablePos = 0; blockLen = 0; continue; }
                result.Append(GetRandom(digits));
                blockLen++;
            }
        }

        return result.ToString()[..Math.Min(length, result.Length)];
    }

    // Генератор с пулом символов
    public static string GenerateFromPool(
        int length,
        bool upper, bool lower, bool digits, bool symbols,
        bool noRepeat)
    {
        var pool = new StringBuilder();
        if (upper)   pool.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        if (lower)   pool.Append("abcdefghijklmnopqrstuvwxyz");
        if (digits)  pool.Append("0123456789");
        if (symbols) pool.Append("!\"№;%:?*()_+");

        string poolStr = pool.ToString();
        if (poolStr.Length == 0) return "(выберите набор символов)";
        if (noRepeat && length > poolStr.Length)
            return $"(длина > размер пула: {poolStr.Length})";

        char[] chars = poolStr.ToCharArray();

        if (noRepeat)
        {
            // Fisher-Yates shuffle
            for (int k = chars.Length - 1; k > 0; k--)
            {
                int j = (int)(RandomNumberGenerator.GetInt32(k + 1));
                (chars[k], chars[j]) = (chars[j], chars[k]);
            }
            return new string(chars[..length]);
        }
        else
        {
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
                sb.Append(chars[RandomNumberGenerator.GetInt32(chars.Length)]);
            return sb.ToString();
        }
    }

    private static char GetRandom(char[] arr) =>
        arr[RandomNumberGenerator.GetInt32(arr.Length)];
}