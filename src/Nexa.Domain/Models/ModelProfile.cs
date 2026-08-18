using Nexa.Domain.Common;

namespace Nexa.Domain.Models;

public sealed class ModelProfile
{
    private ModelProfile()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProviderId { get; private set; }
    public string Name { get; private set; } = null!;
    public string ModelId { get; private set; } = null!;
    public double? Temperature { get; private set; }
    public int? MaxTokens { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ModelProfile Create(
        Guid providerId,
        string name,
        string modelId,
        double? temperature,
        int? maxTokens,
        bool isEnabled = true)
    {
        ValidateSampling(temperature, maxTokens);
        var now = DateTimeOffset.UtcNow;
        return new ModelProfile
        {
            Id = Guid.CreateVersion7(),
            ProviderId = Guard.NotEmpty(providerId, nameof(providerId)),
            Name = Guard.NotNullOrWhiteSpace(name, nameof(name), 100),
            ModelId = Guard.NotNullOrWhiteSpace(modelId, nameof(modelId), 200),
            Temperature = temperature,
            MaxTokens = maxTokens,
            IsEnabled = isEnabled,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string name, string modelId, double? temperature, int? maxTokens, bool isEnabled)
    {
        ValidateSampling(temperature, maxTokens);
        Name = Guard.NotNullOrWhiteSpace(name, nameof(name), 100);
        ModelId = Guard.NotNullOrWhiteSpace(modelId, nameof(modelId), 200);
        Temperature = temperature;
        MaxTokens = maxTokens;
        IsEnabled = isEnabled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateSampling(double? temperature, int? maxTokens)
    {
        if (temperature is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(temperature), "Temperature must be between 0 and 2.");
        }

        if (maxTokens is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "MaxTokens must be greater than zero.");
        }
    }
}
