using Maf.Agent.Api.Knowledge.Application;
using Microsoft.Extensions.Options;
using PDFtoImage;
using Tesseract;
using UglyToad.PdfPig;

namespace Maf.Agent.Api.Knowledge.Infrastructure.Extractors;

public sealed class OcrOptions
{
    public string TessDataPath { get; set; } = "tessdata";
    public string Languages { get; set; } = "fas+eng";
    public int Dpi { get; set; } = 300;
}

public sealed class PdfTextExtractor(IOptions<OcrOptions> options)
    : IDocumentTextExtractor
{
    private const int MinimumExtractedCharacters = 20;
    private readonly OcrOptions _options = options.Value;

    public bool CanExtract(string fileName, string contentType) =>
        Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
        contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

    public Task<IReadOnlyList<ExtractedSection>> ExtractAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        List<(int PageNumber, string Text)> pages = [];
        using (PdfDocument document = PdfDocument.Open(content))
        {
            foreach (UglyToad.PdfPig.Content.Page page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                pages.Add((page.Number, page.Text.Trim()));
            }
        }

        List<ExtractedSection> sections = [];
        foreach ((int pageNumber, string extractedText) in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string text = extractedText;
            if (text.Length < MinimumExtractedCharacters)
            {
                text = OcrPage(content, pageNumber - 1);
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                sections.Add(new ExtractedSection(
                    text.Trim(),
                    PageNumber: pageNumber,
                    Section: $"Page {pageNumber}"));
            }
        }

        return Task.FromResult<IReadOnlyList<ExtractedSection>>(sections);
    }

    private string OcrPage(Stream pdf, int zeroBasedPage)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Local PDF OCR is supported only on Windows and Linux.");
        }

        if (!Directory.Exists(_options.TessDataPath))
        {
            throw new InvalidOperationException(
                $"OCR is required but tessdata was not found at '{_options.TessDataPath}'.");
        }

        if (pdf.CanSeek)
        {
            pdf.Position = 0;
        }

        using MemoryStream image = new();
#pragma warning disable CA1416 // Guarded above to the supported Windows/Linux targets.
        Conversion.SavePng(
            image,
            pdf,
            zeroBasedPage,
            leaveOpen: true,
            password: null,
            options: new RenderOptions
            {
                Dpi = _options.Dpi,
                Grayscale = true,
            });
#pragma warning restore CA1416

        byte[] imageBytes = image.ToArray();
        using Pix pix = Pix.LoadFromMemory(imageBytes);
        using TesseractEngine engine = new(
            _options.TessDataPath,
            _options.Languages,
            EngineMode.Default);
        using Page page = engine.Process(pix);
        return page.GetText();
    }
}
