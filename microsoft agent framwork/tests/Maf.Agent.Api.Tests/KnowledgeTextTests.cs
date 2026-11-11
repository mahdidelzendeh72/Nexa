using Maf.Agent.Api.Knowledge.Application;
using Maf.Agent.Api.Knowledge.Infrastructure;

namespace Maf.Agent.Api.Tests;

public sealed class KnowledgeTextTests
{
    [Fact]
    public void Normalizer_unifies_Persian_characters_and_whitespace()
    {
        PersianTextNormalizer normalizer = new();

        string result = normalizer.Normalize("  كِتاب\u200Cهاي   يک  ");

        Assert.Equal("کتاب های یک", result);
    }

    [Fact]
    public void Chunker_preserves_metadata_and_creates_overlap()
    {
        StructureAwareKnowledgeChunker chunker =
            new(new PersianTextNormalizer());
        string content = string.Concat(
            Enumerable.Repeat("این یک جمله آزمایشی برای قطعه بندی است. ", 150));

        IReadOnlyList<KnowledgeChunkDraft> chunks = chunker.CreateChunks(
            [new ExtractedSection(content, PageNumber: 7, Section: "بخش")]);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk =>
        {
            Assert.Equal(7, chunk.PageNumber);
            Assert.Equal("بخش", chunk.Section);
            Assert.NotEmpty(chunk.NormalizedContent);
        });
        Assert.Contains(
            chunks[0].Content[^100..],
            chunks[1].Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Rrf_rewards_items_present_in_multiple_rankings()
    {
        IReadOnlyDictionary<long, double> scores =
            ReciprocalRankFusion.Combine<long>(
            [
                [1, 2, 3],
                [2, 4, 1],
            ]);

        Assert.True(scores[2] > scores[1]);
        Assert.True(scores[1] > scores[3]);
        Assert.Equal(4, scores.Count);
    }
}
