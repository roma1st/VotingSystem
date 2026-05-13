using AutoFixture;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;

namespace VotingSystem.UnitTests.Services;

public class ElectionService_CreateTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task CreateElection_SetsStatusToDraft()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var sut = TestHelpers.CreateService(ctx);

        var dto = new CreateElectionDto(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            ElectionType.SingleChoice);

        var result = await sut.CreateElectionAsync(dto);

        Assert.Equal(ElectionStatus.Draft, result.Status);
    }

    [Fact]
    public async Task CreateElection_AssignsNonEmptyId()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var sut = TestHelpers.CreateService(ctx);

        var dto = new CreateElectionDto("T", "D", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), ElectionType.SingleChoice);

        var result = await sut.CreateElectionAsync(dto);

        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task CreateElection_ConvertsDatesToUtc()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var sut = TestHelpers.CreateService(ctx);

        var local = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local);
        var dto = new CreateElectionDto("T", "D", local, local.AddDays(1), ElectionType.SingleChoice);

        var result = await sut.CreateElectionAsync(dto);

        Assert.Equal(DateTimeKind.Utc, result.StartDate.Kind);
        Assert.Equal(DateTimeKind.Utc, result.EndDate.Kind);
    }

    [Theory]
    [InlineData(ElectionType.SingleChoice)]
    [InlineData(ElectionType.RankedChoice)]
    public async Task CreateElection_PreservesType(ElectionType type)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var sut = TestHelpers.CreateService(ctx);

        var dto = new CreateElectionDto("T", "D", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), type);

        var result = await sut.CreateElectionAsync(dto);

        Assert.Equal(type, result.Type);
    }

    [Fact]
    public async Task CreateElection_ReturnsEmptyCandidates()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var sut = TestHelpers.CreateService(ctx);

        var dto = new CreateElectionDto("T", "D", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), ElectionType.SingleChoice);

        var result = await sut.CreateElectionAsync(dto);

        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task CreateElection_PersistsToDatabase()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var sut = TestHelpers.CreateService(ctx);

        var dto = new CreateElectionDto("Persisted", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), ElectionType.SingleChoice);
        await sut.CreateElectionAsync(dto);

        Assert.Equal(1, ctx.Elections.Count());
    }
}
