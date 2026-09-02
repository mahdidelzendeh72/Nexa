using FluentAssertions;
using Nexa.Domain.Agents;

namespace Nexa.Domain.Tests;

public sealed class AgentTests
{
    [Fact]
    public void Create_starts_at_version_one()
    {
        var agent = Agent.Create(Guid.CreateVersion7(), "Hotel Agent", "Helps with hotels", "You are a hotel agent.", Guid.CreateVersion7());

        agent.CurrentVersion.VersionNumber.Should().Be(1);
        agent.Versions.Should().HaveCount(1);
    }

    [Fact]
    public void PublishVersion_increments_monotonically()
    {
        var profile = Guid.CreateVersion7();
        var agent = Agent.Create(Guid.CreateVersion7(), "Hotel Agent", "", "v1", profile);

        var v2 = agent.PublishVersion("v2", profile);
        var v3 = agent.PublishVersion("v3", Guid.CreateVersion7());

        v2.VersionNumber.Should().Be(2);
        v3.VersionNumber.Should().Be(3);
        agent.CurrentVersion.Id.Should().Be(v3.Id);
        agent.Versions.Should().HaveCount(3);
    }
}
