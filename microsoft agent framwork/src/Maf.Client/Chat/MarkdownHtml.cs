using System.Globalization;
using System.Text;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Maf.Client.Chat;

public static class MarkdownHtml
{
    private const string CopyIcon =
        "<svg class=\"icon-copy\" viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" aria-hidden=\"true\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><rect x=\"9\" y=\"9\" width=\"13\" height=\"13\" rx=\"2\"></rect><path d=\"M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1\"></path></svg>";

    private const string CheckIcon =
        "<svg class=\"icon-check\" viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" aria-hidden=\"true\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M20 6 9 17l-5-5\"></path></svg>";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .DisableHtml()
        .Use(new CopyableCodeBlockExtension())
        .Build();

    public static string Render(string? markdown) =>
        string.IsNullOrEmpty(markdown) ? "" : Markdown.ToHtml(markdown, Pipeline);

    private sealed class CopyableCodeBlockExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is not HtmlRenderer html)
                return;

            for (var index = html.ObjectRenderers.Count - 1; index >= 0; index--)
            {
                if (html.ObjectRenderers[index] is CodeBlockRenderer)
                    html.ObjectRenderers.RemoveAt(index);
            }

            html.ObjectRenderers.AddIfNotAlready<CopyableCodeBlockRenderer>();
        }
    }

    private sealed class CopyableCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
    {
        protected override void Write(HtmlRenderer renderer, CodeBlock block)
        {
            var language = block is FencedCodeBlock fenced ? LanguageOf(fenced.Info) : null;
            var lines = ReadLines(block);
            var svg = SvgSource(language, lines);

            renderer.EnsureLine();
            renderer.Write("<div class=\"code-block");
            if (svg is not null)
                renderer.Write(" code-block-svg");
            renderer.Write("\" dir=\"ltr\"><div class=\"code-block-bar\"><span class=\"code-block-lang\">");
            if (language is not null)
                renderer.WriteEscape(language);
            renderer.Write("</span>");
            if (svg is not null)
            {
                renderer.Write("<div class=\"code-block-actions\"><div class=\"code-block-switch\">");
                renderer.Write("<button type=\"button\" class=\"code-block-mode\" aria-pressed=\"false\" onclick=\"mafMarkdown.show(this,'code')\">کد</button>");
                renderer.Write("<button type=\"button\" class=\"code-block-mode is-active\" aria-pressed=\"true\" onclick=\"mafMarkdown.show(this,'preview')\">پیش‌نمایش</button>");
                renderer.Write("</div>");
                WriteCopyButton(renderer);
                renderer.Write("</div>");
            }
            else
            {
                WriteCopyButton(renderer);
            }

            renderer.Write("</div>");
            if (svg is not null)
            {
                renderer.Write("<div class=\"code-block-preview\"><img alt=\"پیش‌نمایش\" src=\"data:image/svg+xml;base64,");
                renderer.Write(Convert.ToBase64String(Encoding.UTF8.GetBytes(svg)));
                renderer.Write("\" /></div>");
            }

            renderer.Write("<div class=\"code-block-body\"><div class=\"code-block-gutter\" aria-hidden=\"true\">");
            for (var index = 0; index < lines.Count; index++)
            {
                if (index > 0)
                    renderer.Write('\n');
                renderer.Write((index + 1).ToString(CultureInfo.InvariantCulture));
            }

            renderer.Write("</div><pre><code>");
            for (var index = 0; index < lines.Count; index++)
            {
                if (index > 0)
                    renderer.Write('\n');
                renderer.WriteEscape(lines[index]);
            }

            renderer.Write("</code></pre></div></div>");
        }

        private static void WriteCopyButton(HtmlRenderer renderer)
        {
            renderer.Write("<button type=\"button\" class=\"code-block-copy\" title=\"کپی\" aria-label=\"کپی\" onclick=\"mafMarkdown.copy(this)\">");
            renderer.Write(CopyIcon);
            renderer.Write(CheckIcon);
            renderer.Write("</button>");
        }

        private static string? SvgSource(string? language, List<string> lines)
        {
            if (language is null || !language.Equals("svg", StringComparison.OrdinalIgnoreCase))
                return null;

            var source = string.Join('\n', lines);
            return source.Contains("<svg", StringComparison.OrdinalIgnoreCase) ? source : null;
        }

        private static List<string> ReadLines(CodeBlock block)
        {
            var lines = new List<string>(block.Lines.Count);
            var slices = block.Lines.Lines;
            for (var index = 0; index < block.Lines.Count; index++)
                lines.Add(slices[index].ToString());
            return lines;
        }

        private static string? LanguageOf(string? info)
        {
            if (string.IsNullOrWhiteSpace(info))
                return null;

            var end = 0;
            while (end < info.Length && !char.IsWhiteSpace(info[end]))
                end++;

            return info[..end];
        }
    }
}
