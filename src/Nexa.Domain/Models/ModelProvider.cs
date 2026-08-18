using Nexa.Domain.Common;

namespace Nexa.Domain.Models;

public sealed class ModelProvider
{
    private ModelProvider()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public ModelProviderKind Kind { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ModelProvider Create(string name, ModelProviderKind kind, bool isEnabled = true)
    {
        var now = DateTimeOffset.UtcNow;
        return new ModelProvider
        {
            Id = Guid.CreateVersion7(),
            Name = Guard.NotNullOrWhiteSpace(name, nameof(name), 100),
            Kind = kind,
            IsEnabled = isEnabled,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Rename(string name)
    {
        Name = Guard.NotNullOrWhiteSpace(name, nameof(name), 100);
        Touch();
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
