namespace Maf.Agent.Api.Chat.Domain;

public sealed class ChatToolMessage
{
    public const int MaxPayloadLength = 500_000;

    private ChatToolMessage()
    {
        Payload = "";
    }

    internal ChatToolMessage(string payload, int sequence)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > MaxPayloadLength)
        {
            throw new ArgumentException("پیام ابزار برای نگهداری معتبر نیست.");
        }

        Payload = payload;
        Sequence = sequence;
    }

    public long Id { get; private set; }

    public long EntryId { get; private set; }

    public string Payload { get; private set; }

    public int Sequence { get; private set; }
}
