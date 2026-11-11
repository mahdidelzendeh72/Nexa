using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Maf.Agent.Api.Knowledge.Application;

namespace Maf.Agent.Api.Knowledge.Infrastructure;

public sealed partial class PersianTextNormalizer : ITextNormalizer
{
    public string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string normalized = text
            .Normalize(NormalizationForm.FormKC)
            .Replace('\u064A', '\u06CC')
            .Replace('\u0649', '\u06CC')
            .Replace('\u0643', '\u06A9')
            .Replace('\u200C', ' ');

        StringBuilder builder = new(normalized.Length);
        foreach (char character in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category is not UnicodeCategory.NonSpacingMark and
                not UnicodeCategory.EnclosingMark)
            {
                builder.Append(character);
            }
        }

        return WhitespaceRegex()
            .Replace(builder.ToString(), " ")
            .Trim()
            .ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
