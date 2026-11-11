using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Maf.Agent.Api.Knowledge.Application;

namespace Maf.Agent.Api.Knowledge.Infrastructure.Extractors;

public sealed class DocxTextExtractor : IDocumentTextExtractor
{
    public bool CanExtract(string fileName, string contentType) =>
        Path.GetExtension(fileName).Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            StringComparison.OrdinalIgnoreCase);

    public Task<IReadOnlyList<ExtractedSection>> ExtractAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using WordprocessingDocument document =
            WordprocessingDocument.Open(content, false);
        Body? body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return Task.FromResult<IReadOnlyList<ExtractedSection>>([]);
        }

        List<ExtractedSection> sections = [];
        StringBuilder current = new();
        string? heading = null;

        foreach (OpenXmlElement element in body.Elements())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (element is Paragraph paragraph)
            {
                string text = paragraph.InnerText.Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                string? style =
                    paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                if (style?.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) is true)
                {
                    FlushSection(sections, current, heading);
                    heading = text;
                    continue;
                }

                current.AppendLine(text);
            }
            else if (element is Table table)
            {
                foreach (TableRow row in table.Elements<TableRow>())
                {
                    string rowText = string.Join(
                        " | ",
                        row.Elements<TableCell>()
                            .Select(cell => cell.InnerText.Trim()));
                    if (rowText.Length > 0)
                    {
                        current.AppendLine(rowText);
                    }
                }
            }
        }

        FlushSection(sections, current, heading);
        return Task.FromResult<IReadOnlyList<ExtractedSection>>(sections);
    }

    private static void FlushSection(
        ICollection<ExtractedSection> sections,
        StringBuilder content,
        string? heading)
    {
        if (content.Length == 0 && string.IsNullOrWhiteSpace(heading))
        {
            return;
        }

        string text = content.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(heading))
        {
            text = $"{heading}{Environment.NewLine}{text}".Trim();
        }

        sections.Add(new ExtractedSection(text, Section: heading));
        content.Clear();
    }
}
