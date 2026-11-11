using System.Text;
using Maf.Agent.Api.Knowledge.Application;

namespace Maf.Agent.Api.Knowledge.Infrastructure.Extractors;

public sealed class PlainTextExtractor : IDocumentTextExtractor
{
    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { ".txt", ".md", ".markdown" };

    public bool CanExtract(string fileName, string contentType) =>
        Extensions.Contains(Path.GetExtension(fileName)) ||
        contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<ExtractedSection>> ExtractAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using StreamReader reader = new(
            content,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        string text = await reader.ReadToEndAsync(cancellationToken);
        return [new ExtractedSection(text)];
    }
}
