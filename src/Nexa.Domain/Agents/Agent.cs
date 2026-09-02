using Nexa.Domain.Common;

namespace Nexa.Domain.Agents;

public sealed class Agent
{
    private readonly List<AgentVersion> _versions = [];

    private Agent()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public Guid OwnerUserId { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<AgentVersion> Versions => _versions;

    public AgentVersion CurrentVersion =>
        _versions.Count == 0
            ? throw new InvalidOperationException("Agent has no versions.")
            : _versions.MaxBy(v => v.VersionNumber)!;

    public static Agent Create(
        Guid ownerUserId,
        string name,
        string description,
        string instructions,
        Guid modelProfileId,
        ChatLimits? limits = null)
    {
        var now = DateTimeOffset.UtcNow;
        var agent = new Agent
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = Guard.NotEmpty(ownerUserId, nameof(ownerUserId)),
            Name = Guard.NotNullOrWhiteSpace(name, nameof(name), 120),
            Description = Guard.Optional(description, 2000),
            CreatedAt = now,
            UpdatedAt = now
        };

        agent._versions.Add(AgentVersion.Create(
            agent.Id,
            1,
            instructions,
            modelProfileId,
            limits ?? ChatLimits.None,
            now));

        return agent;
    }

    public void Rename(string name, string description)
    {
        Name = Guard.NotNullOrWhiteSpace(name, nameof(name), 120);
        Description = Guard.Optional(description, 2000);
        Touch();
    }

    public AgentVersion PublishVersion(string instructions, Guid modelProfileId, ChatLimits? limits = null)
    {
        var next = CurrentVersion.VersionNumber + 1;
        var version = AgentVersion.Create(
            Id,
            next,
            instructions,
            modelProfileId,
            limits ?? ChatLimits.None,
            DateTimeOffset.UtcNow);
        _versions.Add(version);
        Touch();
        return version;
    }

    public void Archive()
    {
        IsArchived = true;
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        RowVersion++;
    }
}
