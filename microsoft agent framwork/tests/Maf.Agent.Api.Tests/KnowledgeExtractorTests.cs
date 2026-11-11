using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Maf.Agent.Api.Knowledge.Application;
using Maf.Agent.Api.Knowledge.Infrastructure.Extractors;

namespace Maf.Agent.Api.Tests;

public sealed class KnowledgeExtractorTests
{
    [Theory]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("readme.md", "text/markdown")]
    public async Task Plain_text_extractor_reads_supported_files(
        string fileName,
        string contentType)
    {
        PlainTextExtractor extractor = new();
        await using MemoryStream stream =
            new("دانش آزمایشی"u8.ToArray());

        IReadOnlyList<ExtractedSection> sections =
            await extractor.ExtractAsync(stream, CancellationToken.None);

        Assert.True(extractor.CanExtract(fileName, contentType));
        Assert.Single(sections);
        Assert.Equal("دانش آزمایشی", sections[0].Content);
    }

    [Fact]
    public async Task Docx_extractor_preserves_heading_as_section()
    {
        await using MemoryStream stream = new();
        using (WordprocessingDocument document =
               WordprocessingDocument.Create(
                   stream,
                   DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
                   autoSave: true))
        {
            MainDocumentPart main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(
                new Paragraph(
                    new ParagraphProperties(
                        new ParagraphStyleId { Val = "Heading1" }),
                    new Run(new Text("عنوان"))),
                new Paragraph(new Run(new Text("محتوای سند")))));
        }

        DocxTextExtractor extractor = new();
        IReadOnlyList<ExtractedSection> sections =
            await extractor.ExtractAsync(stream, CancellationToken.None);

        ExtractedSection section = Assert.Single(sections);
        Assert.Equal("عنوان", section.Section);
        Assert.Contains("محتوای سند", section.Content);
    }

    [Fact]
    public async Task Xlsx_extractor_preserves_sheet_and_cells()
    {
        await using MemoryStream stream = new();
        using (XLWorkbook workbook = new())
        {
            IXLWorksheet sheet = workbook.AddWorksheet("دانش");
            sheet.Cell(1, 1).Value = "کلید";
            sheet.Cell(1, 2).Value = "مقدار";
            sheet.Cell(2, 1).Value = "محصول";
            sheet.Cell(2, 2).Value = "MAF";
            workbook.SaveAs(stream);
        }

        XlsxTextExtractor extractor = new();
        IReadOnlyList<ExtractedSection> sections =
            await extractor.ExtractAsync(stream, CancellationToken.None);

        ExtractedSection section = Assert.Single(sections);
        Assert.Equal("دانش", section.SheetName);
        Assert.Contains("کلید: محصول", section.Content);
        Assert.Contains("مقدار: MAF", section.Content);
    }
}
