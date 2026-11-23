using System.Text;

namespace Semojum.Desktop.Controls;

/// <summary>One-line notation for elementary arithmetic. Other TeX keeps its typeset drawing.</summary>
internal sealed class FormulaLine(string source, string fractionSlash)
{
    private int index;
    private readonly record struct Part(string Text, bool Compound = false)
    {
        internal string Grouped => Compound ? "(" + Text + ")" : Text;
    }

    internal static string? Format(string latex, string slash)
    {
        if (latex.Length > 2048) return null;
        var reader = new FormulaLine(latex, slash);
        return reader.Expression(0) is { } part && reader.index == latex.Length ? part.Text : null;
    }

    private Part? Expression(int depth)
    {
        if (depth > 16 || Operand(depth) is not { } first) return null;
        var text = new StringBuilder(first.Text);
        var compound = first.Compound;
        while (true)
        {
            SkipSpaces();
            if (index == source.Length || source[index] == '}') return new(text.ToString(), compound);
            string? op = Take("+") ? "+" : Take("-") ? "−" : Take("=") ? "=" : Take(@"\times") ? "×" : null;
            if (op is null || Operand(depth) is not { } next) return null;
            text.Append(' ').Append(op).Append(' ').Append(next.Text);
            compound = true;
        }
    }

    private Part? Operand(int depth)
    {
        SkipSpaces();
        if (index == source.Length) return null;
        Part value;
        if (Take(@"\frac"))
        {
            if (Group(depth) is not { } top || Group(depth) is not { } bottom) return null;
            value = new(top.Grouped + " " + fractionSlash + " " + bottom.Grouped, true);
        }
        else if (Take(@"\sqrt"))
        {
            if (Group(depth) is not { } root) return null;
            value = new("√" + root.Grouped);
        }
        else if (Take(@"\text"))
        {
            SkipSpaces();
            if (!Take("{")) return null;
            var start = index;
            while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == ' ')) index++;
            var label = source[start..index];
            if (string.IsNullOrWhiteSpace(label) || !Take("}")) return null;
            value = new(label);
        }
        else if (char.IsAsciiLetter(source[index])) value = new(source[index++].ToString());
        else
        {
            var start = index;
            while (index < source.Length && char.IsAsciiDigit(source[index])) index++;
            if (index == start) return null;
            if (Take("."))
            {
                var decimalStart = index;
                while (index < source.Length && char.IsAsciiDigit(source[index])) index++;
                if (index == decimalStart) return null;
            }
            value = new(source[start..index]);
        }
        SkipSpaces();
        if (Take(@"(\%)")) return new(value.Grouped + "(%)");
        if (Take(@"\%")) return new(value.Grouped + "%");
        return value;
    }

    private Part? Group(int depth)
    {
        SkipSpaces();
        if (!Take("{") || Expression(depth + 1) is not { } part || !Take("}")) return null;
        return part;
    }

    private void SkipSpaces()
    {
        while (index < source.Length && char.IsWhiteSpace(source[index])) index++;
    }

    private bool Take(string token)
    {
        if (!source.AsSpan(index).StartsWith(token, StringComparison.Ordinal)) return false;
        index += token.Length;
        return true;
    }
}
