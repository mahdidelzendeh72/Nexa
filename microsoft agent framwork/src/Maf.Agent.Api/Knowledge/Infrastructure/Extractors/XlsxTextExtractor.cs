using System.Text;
using ClosedXML.Excel;
using Maf.Agent.Api.Knowledge.Application;

namespace Maf.Agent.Api.Knowledge.Infrastructure.Extractors;

public sealed class XlsxTextExtractor : IDocumentTextExtractor
{
    public bool CanExtract(string fileName, string contentType) =>
        Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            StringComparison.OrdinalIgnoreCase);

    public Task<IReadOnlyList<ExtractedSection>> ExtractAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using XLWorkbook workbook = new(content);
        List<ExtractedSection> sections = [];

        foreach (IXLWorksheet worksheet in workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IXLRange? range = worksheet.RangeUsed();
            if (range is null)
            {
                continue;
            }

            int firstColumn = range.RangeAddress.FirstAddress.ColumnNumber;
            int lastColumn = range.RangeAddress.LastAddress.ColumnNumber;
            IXLRangeRow headerRow = range.FirstRow();
            string[] headers = Enumerable
                .Range(firstColumn, lastColumn - firstColumn + 1)
                .Select(column =>
                {
                    string value = headerRow.Cell(column - firstColumn + 1)
                        .GetFormattedString()
                        .Trim();
                    return value.Length == 0
                        ? $"Column {column}"
                        : value;
                })
                .ToArray();

            StringBuilder text = new();
            foreach (IXLRangeRow row in range.Rows())
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<string> values = [];
                for (int index = 0; index < headers.Length; index++)
                {
                    string value = row.Cell(index + 1).GetFormattedString().Trim();
                    if (value.Length > 0)
                    {
                        values.Add($"{headers[index]}: {value}");
                    }
                }

                if (values.Count > 0)
                {
                    text.AppendLine(string.Join(" | ", values));
                }
            }

            if (text.Length > 0)
            {
                sections.Add(new ExtractedSection(
                    text.ToString(),
                    SheetName: worksheet.Name,
                    Section: worksheet.Name));
            }
        }

        return Task.FromResult<IReadOnlyList<ExtractedSection>>(sections);
    }
}
