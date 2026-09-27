namespace Upms.Domain.Common;

/// <summary>Fractional indexing for card order (research R14): keys are base-62 strings compared
/// ordinally, and a key can always be generated between any two others, so a move writes one row. Keys
/// have an integer part whose length grows only logarithmically when cards are appended or prepended.
/// Based on the algorithm published by David Greenspan ("Implementing Fractional Indexing").</summary>
public static class Rank
{
    /// <summary>Longest key the database column holds.</summary>
    public const int MaxLength = 64;

    /// <summary>Keys longer than this make their column due for rebalancing.</summary>
    public const int RebalanceThreshold = 48;

    private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const char ZeroDigit = '0';
    private const char MaxDigit = 'z';
    private static readonly string SmallestInteger = "A" + new string(ZeroDigit, 26);

    /// <summary>The key for the first card of an empty column.</summary>
    public static string First() => "a0";

    /// <summary>A key after <paramref name="key"/> (or the first key when it is null).</summary>
    public static string After(string? key) => Between(key, null);

    /// <summary>A key before <paramref name="key"/> (or the first key when it is null).</summary>
    public static string Before(string? key) => Between(null, key);

    public static bool NeedsRebalance(string key) => key.Length > RebalanceThreshold;

    /// <summary>A key strictly between two keys; null means "no bound" on that side.</summary>
    public static string Between(string? before, string? after)
    {
        if (before is not null)
        {
            Validate(before);
        }

        if (after is not null)
        {
            Validate(after);
        }

        if (before is not null && after is not null && string.CompareOrdinal(before, after) >= 0)
        {
            throw new ArgumentException($"'{before}' must sort before '{after}'.", nameof(before));
        }

        if (before is null)
        {
            if (after is null)
            {
                return First();
            }

            var integerAfter = IntegerPart(after);
            var fractionAfter = after[integerAfter.Length..];
            if (integerAfter == SmallestInteger)
            {
                return integerAfter + Midpoint("", fractionAfter);
            }

            if (string.CompareOrdinal(integerAfter, after) < 0)
            {
                return integerAfter;
            }

            return Decrement(integerAfter) ?? throw new InvalidOperationException("No key sorts before " + after);
        }

        var integerBefore = IntegerPart(before);
        var fractionBefore = before[integerBefore.Length..];
        if (after is null)
        {
            var next = Increment(integerBefore);
            return next ?? integerBefore + Midpoint(fractionBefore, null);
        }

        var integerOfAfter = IntegerPart(after);
        var fractionOfAfter = after[integerOfAfter.Length..];
        if (integerBefore == integerOfAfter)
        {
            return integerBefore + Midpoint(fractionBefore, fractionOfAfter);
        }

        var incremented = Increment(integerBefore) ?? throw new InvalidOperationException("No key sorts after " + before);
        return string.CompareOrdinal(incremented, after) < 0 ? incremented : integerBefore + Midpoint(fractionBefore, null);
    }

    /// <summary><paramref name="count"/> increasing keys with short integer parts, for rebalancing.</summary>
    public static IReadOnlyList<string> Sequence(int count)
    {
        var keys = new List<string>(count);
        var key = First();
        for (var i = 0; i < count; i++)
        {
            keys.Add(key);
            key = After(key);
        }

        return keys;
    }

    private static string Midpoint(string a, string? b)
    {
        // a < b, both are fractional parts without trailing zeros; b == null means "no upper bound".
        if (b is not null)
        {
            var n = 0;
            while ((n < a.Length ? a[n] : ZeroDigit) == b[n])
            {
                n++;
            }

            if (n > 0)
            {
                return b[..n] + Midpoint(n < a.Length ? a[n..] : "", b[n..]);
            }
        }

        var digitA = a.Length > 0 ? Digits.IndexOf(a[0], StringComparison.Ordinal) : 0;
        var digitB = b is not null ? Digits.IndexOf(b[0], StringComparison.Ordinal) : Digits.Length;
        if (digitB - digitA > 1)
        {
            return Digits[(digitA + digitB + 1) / 2].ToString();
        }

        if (b is not null && b.Length > 1)
        {
            return b[..1];
        }

        return Digits[digitA] + Midpoint(a.Length > 1 ? a[1..] : "", null);
    }

    private static int IntegerLength(char head) => head switch
    {
        >= 'a' and <= 'z' => head - 'a' + 2,
        >= 'A' and <= 'Z' => 'Z' - head + 2,
        _ => throw new ArgumentException($"Invalid rank key head '{head}'."),
    };

    private static string IntegerPart(string key)
    {
        var length = IntegerLength(key[0]);
        return length <= key.Length ? key[..length] : throw new ArgumentException($"Invalid rank key '{key}'.");
    }

    private static void Validate(string key)
    {
        if (key.Length == 0 || key == SmallestInteger)
        {
            throw new ArgumentException($"Invalid rank key '{key}'.");
        }

        var integer = IntegerPart(key);
        if (key.Length > integer.Length && key[^1] == ZeroDigit)
        {
            throw new ArgumentException($"Invalid rank key '{key}': trailing zero.");
        }
    }

    private static string? Increment(string integer)
    {
        var head = integer[0];
        var digits = integer[1..].ToCharArray();
        var carry = true;
        for (var i = digits.Length - 1; carry && i >= 0; i--)
        {
            var d = Digits.IndexOf(digits[i], StringComparison.Ordinal) + 1;
            if (d == Digits.Length)
            {
                digits[i] = ZeroDigit;
            }
            else
            {
                digits[i] = Digits[d];
                carry = false;
            }
        }

        if (!carry)
        {
            return head + new string(digits);
        }

        if (head == 'Z')
        {
            return "a" + ZeroDigit;
        }

        if (head == 'z')
        {
            return null;
        }

        var newHead = (char)(head + 1);
        var body = new string(digits);
        return newHead > 'a' ? newHead + body + ZeroDigit : newHead + body[..^1];
    }

    private static string? Decrement(string integer)
    {
        var head = integer[0];
        var digits = integer[1..].ToCharArray();
        var borrow = true;
        for (var i = digits.Length - 1; borrow && i >= 0; i--)
        {
            var d = Digits.IndexOf(digits[i], StringComparison.Ordinal) - 1;
            if (d == -1)
            {
                digits[i] = MaxDigit;
            }
            else
            {
                digits[i] = Digits[d];
                borrow = false;
            }
        }

        if (!borrow)
        {
            return head + new string(digits);
        }

        if (head == 'a')
        {
            return "Z" + MaxDigit;
        }

        if (head == 'A')
        {
            return null;
        }

        var newHead = (char)(head - 1);
        var body = new string(digits);
        return newHead < 'Z' ? newHead + body + MaxDigit : newHead + body[..^1];
    }
}
