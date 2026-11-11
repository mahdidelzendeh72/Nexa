using Maf.Agent.Api.Knowledge.Application;

namespace Maf.Agent.Api.Knowledge.Infrastructure;

public sealed class StructureAwareKnowledgeChunker(ITextNormalizer normalizer)
    : IKnowledgeChunker
{
    private const int TargetCharacters = 3000;
    private const int OverlapCharacters = 300;

    public IReadOnlyList<KnowledgeChunkDraft> CreateChunks(
        IReadOnlyList<ExtractedSection> sections)
    {
        List<KnowledgeChunkDraft> chunks = [];
        int ordinal = 0;

        foreach (ExtractedSection section in sections)
        {
            string content = section.Content.Trim();
            if (content.Length == 0)
            {
                continue;
            }

            int start = 0;
            while (start < content.Length)
            {
                int length = Math.Min(TargetCharacters, content.Length - start);
                int end = FindBoundary(content, start, length);
                string chunkText = content[start..end].Trim();

                if (chunkText.Length > 0)
                {
                    chunks.Add(new KnowledgeChunkDraft(
                        ordinal++,
                        chunkText,
                        normalizer.Normalize(chunkText),
                        EstimateTokens(chunkText),
                        section.PageNumber,
                        section.SheetName,
                        section.Section));
                }

                if (end >= content.Length)
                {
                    break;
                }

                start = Math.Max(start + 1, end - OverlapCharacters);
            }
        }

        return chunks;
    }

    private static int FindBoundary(string content, int start, int length)
    {
        int proposedEnd = start + length;
        if (proposedEnd >= content.Length)
        {
            return content.Length;
        }

        int minimumEnd = start + (length * 3 / 4);
        for (int index = proposedEnd; index >= minimumEnd; index--)
        {
            if (content[index - 1] is '\n' or '.' or '!' or '?' or '؟')
            {
                return index;
            }
        }

        return proposedEnd;
    }

    private static int EstimateTokens(string content) =>
        Math.Max(1, (int)Math.Ceiling(content.Length / 4d));
}
