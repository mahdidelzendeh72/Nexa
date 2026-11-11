namespace Maf.Agent.Api.Chat.Domain;

public sealed class ChatAttachment
{
    private ChatAttachment()
    {
        FileName = "";
        MediaType = "";
        ContentBase64 = "";
    }

    internal ChatAttachment(ChatFile file, int sequence)
    {
        FileName = file.FileName;
        MediaType = file.MediaType;
        ContentBase64 = file.ContentBase64;
        Sequence = sequence;
    }

    public long Id { get; private set; }

    public long EntryId { get; private set; }

    public string FileName { get; private set; }

    public string MediaType { get; private set; }

    public string ContentBase64 { get; private set; }

    public int Sequence { get; private set; }
}
