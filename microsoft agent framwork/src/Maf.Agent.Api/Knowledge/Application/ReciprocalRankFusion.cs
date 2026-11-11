namespace Maf.Agent.Api.Knowledge.Application;

public static class ReciprocalRankFusion
{
    public static IReadOnlyDictionary<T, double> Combine<T>(
        IEnumerable<IReadOnlyList<T>> rankings,
        int rankConstant = 60)
        where T : notnull
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rankConstant);

        Dictionary<T, double> scores = [];
        foreach (IReadOnlyList<T> ranking in rankings)
        {
            for (int index = 0; index < ranking.Count; index++)
            {
                T item = ranking[index];
                double contribution = 1d / (rankConstant + index + 1);
                scores[item] = scores.GetValueOrDefault(item) + contribution;
            }
        }

        return scores;
    }
}
