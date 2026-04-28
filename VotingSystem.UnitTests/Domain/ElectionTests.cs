using AutoFixture;
using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using Xunit;

namespace VotingSystem.UnitTests.Domain;

public class ElectionTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void Election_Status_ShouldBeDraft_ByDefault()
    {
        var election = new Election
        {
            Title = _fixture.Create<string>(),
            Description = _fixture.Create<string>(),
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            Type = ElectionType.SingleChoice
        };

        Assert.Equal(ElectionStatus.Draft, election.Status);
    }

    [Fact]
    public void Election_IsActive_ShouldBeTrue_WhenCurrentDateIsBetweenStartAndEnd()
    {
        var election = new Election
        {
            Title = _fixture.Create<string>(),
            Description = _fixture.Create<string>(),
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            Status = ElectionStatus.Active,
            Type = ElectionType.SingleChoice
        };

        var isActive = election.Status == ElectionStatus.Active && 
                       DateTime.UtcNow >= election.StartDate && 
                       DateTime.UtcNow <= election.EndDate;

        Assert.True(isActive);
    }
}
