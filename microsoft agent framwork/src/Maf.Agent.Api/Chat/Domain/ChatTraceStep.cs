namespace Maf.Agent.Api.Chat.Domain;

public sealed class ChatTraceStep
{
    private ChatTraceStep()
    {
        RunId = "";
        Kind = "";
    }

    internal ChatTraceStep(string runId, int sequence, string kind, string? refId, string? name, int size)
    {
        RunId = runId;
        Sequence = sequence;
        Kind = kind;
        RefId = refId;
        Name = name;
        Size = size;
    }

    public long Id { get; private set; }

    public string RunId { get; private set; }

    public int Sequence { get; private set; }

    public string Kind { get; private set; }

    public string? RefId { get; private set; }

    public string? Name { get; private set; }

    public int Size { get; private set; }
}
