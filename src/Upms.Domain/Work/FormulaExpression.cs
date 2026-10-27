using System.Globalization;
using System.Text;
using Upms.Domain.Common;

namespace Upms.Domain.Work;

/// <summary>A formula field's arithmetic: numbers, <c>+ - * /</c>, brackets and number fields named in braces, such as
/// <c>({Story point estimate} + {Buffer}) * 1.5</c>. A missing value or a division by zero gives no result.</summary>
public static class FormulaExpression
{
    public const string InvalidCode = "InvalidFormula";

    /// <summary>Refuses an expression that does not read, or that names a field not among <paramref name="numberFields"/>.</summary>
    public static DomainError? Check(string expression, IReadOnlyCollection<string> numberFields)
    {
        try
        {
            var parser = new Parser(Tokens(expression), _ => 1m);
            parser.Parse();
            var unknown = parser.References.FirstOrDefault(r => !numberFields.Contains(r, StringComparer.OrdinalIgnoreCase));
            return unknown is null
                ? null
                : DomainError.Invalid("Formula", $"{{{unknown}}} is not a number field of this space.");
        }
        catch (FormatException e)
        {
            return DomainError.Invalid("Formula", e.Message);
        }
    }

    /// <summary>The result, rounded to 4 decimals, or null when a field has no value or a division is by zero.</summary>
    public static decimal? Evaluate(string expression, Func<string, decimal?> valueOf)
    {
        try
        {
            var result = new Parser(Tokens(expression), valueOf).Parse();
            return result is { } value ? Math.Round(value, 4, MidpointRounding.AwayFromZero) : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    /// <summary>The fields an expression names.</summary>
    public static IReadOnlyList<string> References(string expression)
    {
        try
        {
            var parser = new Parser(Tokens(expression), _ => 1m);
            parser.Parse();
            return parser.References;
        }
        catch (FormatException)
        {
            return [];
        }
    }

    public static string RenameReference(string expression, string oldName, string newName)
    {
        var text = new StringBuilder();
        var i = 0;
        while (i < expression.Length)
        {
            var close = expression[i] == '{' ? expression.IndexOf('}', i) : -1;
            if (close > i && string.Equals(expression[(i + 1)..close].Trim(), oldName, StringComparison.OrdinalIgnoreCase))
            {
                text.Append('{').Append(newName).Append('}');
                i = close + 1;
                continue;
            }

            text.Append(expression[i]);
            i++;
        }

        return text.ToString();
    }

    private static List<string> Tokens(string expression)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < expression.Length)
        {
            var c = expression[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c is '+' or '-' or '*' or '/' or '(' or ')')
            {
                tokens.Add(c.ToString());
                i++;
            }
            else if (c == '{')
            {
                var close = expression.IndexOf('}', i);
                if (close < 0)
                {
                    throw new FormatException("A field name is missing its closing }.");
                }

                tokens.Add(expression[i..(close + 1)]);
                i = close + 1;
            }
            else if (char.IsDigit(c) || c == '.')
            {
                var start = i;
                while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.'))
                {
                    i++;
                }

                tokens.Add(expression[start..i]);
            }
            else
            {
                throw new FormatException($"“{c}” can't be used in a formula: use numbers, + - * /, brackets and {{field names}}.");
            }
        }

        return tokens;
    }

    /// <summary>expression := term (('+' | '-') term)*; term := factor (('*' | '/') factor)*;
    /// factor := number | {field} | '(' expression ')' | '-' factor.</summary>
    private sealed class Parser(List<string> tokens, Func<string, decimal?> valueOf)
    {
        private int _at;

        public List<string> References { get; } = [];

        public decimal? Parse()
        {
            if (tokens.Count == 0)
            {
                throw new FormatException("Enter a formula, such as {Story point estimate} * 2.");
            }

            var value = Expression();
            if (_at < tokens.Count)
            {
                throw new FormatException($"Something is wrong near “{tokens[_at]}”.");
            }

            return value;
        }

        private decimal? Expression()
        {
            var value = Term();
            while (Peek() is "+" or "-")
            {
                var op = tokens[_at++];
                var right = Term();
                value = value is { } l && right is { } r ? (op == "+" ? l + r : l - r) : null;
            }

            return value;
        }

        private decimal? Term()
        {
            var value = Factor();
            while (Peek() is "*" or "/")
            {
                var op = tokens[_at++];
                var right = Factor();
                value = value is { } l && right is { } r
                    ? op == "*" ? l * r : r == 0 ? null : l / r
                    : null;
            }

            return value;
        }

        private decimal? Factor()
        {
            var token = Peek() ?? throw new FormatException("The formula ends too early.");
            _at++;
            if (token == "-")
            {
                return -Factor();
            }

            if (token == "(")
            {
                var inner = Expression();
                if (Peek() != ")")
                {
                    throw new FormatException("A bracket is not closed.");
                }

                _at++;
                return inner;
            }

            if (token[0] == '{')
            {
                var name = token[1..^1].Trim();
                if (name.Length == 0)
                {
                    throw new FormatException("Write a field name between { and }.");
                }

                References.Add(name);
                return valueOf(name);
            }

            return decimal.TryParse(token, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw new FormatException($"Something is wrong near “{token}”.");
        }

        private string? Peek() => _at < tokens.Count ? tokens[_at] : null;
    }
}
