namespace Maf.Agent.Api.Models.Domain;

public enum ModelOperation
{
    Vision,
    Embedding,
    SpeechToText,
    TextToSpeech,
    ImageGeneration,
}

public sealed class ModelDefault
{
    private ModelDefault()
    {
    }

    private ModelDefault(ModelOperation operation)
    {
        Operation = operation;
    }

    public ModelOperation Operation { get; private set; }

    public Guid? ProviderId { get; private set; }

    public string? ModelName { get; private set; }

    public static ModelDefault Create(ModelOperation operation) => new(operation);

    public static ModelCapability RequiredCapability(ModelOperation operation) => operation switch
    {
        ModelOperation.Vision => ModelCapability.Vision,
        ModelOperation.Embedding => ModelCapability.TextEmbedding,
        ModelOperation.SpeechToText => ModelCapability.AudioInput,
        ModelOperation.TextToSpeech => ModelCapability.AudioOutput,
        ModelOperation.ImageGeneration => ModelCapability.ImageGeneration,
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    public bool Accepts(ProviderModel? model) =>
        model is not null &&
        string.Equals(ModelName, model.Name, StringComparison.Ordinal) &&
        model.Capabilities.HasFlag(RequiredCapability(Operation));

    public void Assign(Guid providerId, string modelName, ModelCapability capabilities)
    {
        if (providerId == Guid.Empty)
        {
            throw new ArgumentException("ارائه‌دهنده مدل مشخص نشده است.");
        }

        if (string.IsNullOrWhiteSpace(modelName) || modelName.Trim().Length > 200)
        {
            throw new ArgumentException("نام مدل باید بین ۱ تا ۲۰۰ نویسه باشد.");
        }

        if (!capabilities.HasFlag(RequiredCapability(Operation)))
        {
            throw new ArgumentException($"مدل «{modelName.Trim()}» با این عملیات سازگار نیست.");
        }

        ProviderId = providerId;
        ModelName = modelName.Trim();
    }

    public void Clear()
    {
        ProviderId = null;
        ModelName = null;
    }
}
