using System.Globalization;
using System.Text.RegularExpressions;

namespace BazaarPlusPlus.Android;

// TMP rich-text vocabulary shared by every preview builder. One label renders the
// whole preview, so hierarchy comes from tags instead of separate UI objects.
internal static class AndroidPreviewText
{
    internal const string BodyColor = "F3E7CC";
    internal const string HeadingColor = "F2C46B";
    internal const string MutedColor = "B8AC94";
    private const float BulletIndent = .9f;
    // A blank line at reduced size: paragraphs stay distinct at less height.
    internal const string Gap = "\n<size=40%> </size>\n";
    private static readonly Regex IndentOpen = new("<indent=([0-9.]+)em>", RegexOptions.CultureInvariant);

    internal static string Heading(string text) => $"<color=#{HeadingColor}><b>{text}</b></color>";

    internal static string Colored(string text, string hex) => $"<color=#{hex}><b>{text}</b></color>";

    internal static string Muted(string text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : $"<color=#{MutedColor}><size=88%>{text}</size></color>";

    // TMP indents are absolute and </indent> resets to zero, so nested items are
    // shifted by this level before the hanging indent wraps them.
    internal static string Bullet(string text, string marker = "•")
    {
        var level = Em(BulletIndent);
        var nested = IndentOpen.Replace(text, match =>
            $"<indent={Em(float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) + BulletIndent)}em>");
        nested = nested.Replace("</indent>", $"<indent={level}em>");
        return $"{marker}<indent={level}em>{nested}</indent>";
    }

    internal static string Lines(params string?[] lines) =>
        string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));

    internal static string Sections(IEnumerable<string?> sections) =>
        string.Join(Gap, sections.Where(section => !string.IsNullOrWhiteSpace(section)));

    // Returns the value every entry shares when two or more entries repeat it.
    internal static string? Shared(IReadOnlyList<string?> values)
    {
        if (values.Count < 2 || values.Any(string.IsNullOrWhiteSpace)) return null;
        return values.Distinct().Count() == 1 ? values[0] : null;
    }

    // Shop groups usually repeat one day-quality line; show each distinct line
    // once and list the pools it governs beneath it. Nothing is dropped.
    internal static string Grouped(string heading, IReadOnlyList<(string Quality, string Detail)> groups)
    {
        var shared = Shared(groups.Select(group => (string?)group.Quality).ToArray());
        if (shared != null)
            return Lines(Heading(heading), shared,
                string.Join("\n", groups.Select(group => group.Detail).Where(detail => !string.IsNullOrWhiteSpace(detail))
                    .Distinct().Select(detail => Bullet(detail))));
        var sections = new List<string> { Heading(heading) };
        foreach (var quality in groups.Select(group => group.Quality).Distinct())
        {
            var details = groups.Where(group => group.Quality == quality).Select(group => group.Detail)
                .Where(detail => !string.IsNullOrWhiteSpace(detail)).Distinct().Select(detail => Bullet(detail)).ToArray();
            sections.Add(Lines(string.IsNullOrWhiteSpace(quality) ? null : $"<b>{quality}</b>", string.Join("\n", details)));
        }
        return Lines(sections[0], string.Join(Gap, sections.Skip(1).Where(section => !string.IsNullOrWhiteSpace(section))));
    }

    // Pool descriptions wrap the whole conjunction in parentheses; a list item
    // already delimits it.
    internal static string Unwrap(string text)
    {
        if (text.Length < 2 || text[0] != '(' || text[^1] != ')') return text;
        var depth = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '(') depth++;
            else if (text[index] == ')' && --depth == 0 && index < text.Length - 1) return text;
        }
        return text[1..^1];
    }

    private static string Em(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
