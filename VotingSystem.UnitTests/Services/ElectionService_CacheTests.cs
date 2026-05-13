using Microsoft.Extensions.Caching.Memory;
using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.Api.Services;

namespace VotingSystem.UnitTests.Services;

public class ElectionService_CacheTests
{
    [Fact]
    public async Task GetElection_SecondCall_HitsCache_NotDb()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection();
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ElectionService(ctx, cache);

        await sut.GetElectionAsync(election.Id);

        ctx.Elections.Remove(election);
        await ctx.SaveChangesAsync();

        var second = await sut.GetElectionAsync(election.Id);

        Assert.Equal(election.Id, second.Id);
    }

    [Fact]
    public async Task CreateElection_InvalidatesListCache()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ElectionService(ctx, cache);

        var before = await sut.GetElectionsAsync(null);
        Assert.Empty(before);

        await sut.CreateElectionAsync(new CreateElectionDto(
            "T", "D", DateTime.UtcNow, DateTime.UtcNow.AddDays(1), ElectionType.SingleChoice));

        var after = await sut.GetElectionsAsync(null);
        Assert.Single(after);
    }

    [Fact]
    public async Task GetResults_SecondCall_ReturnsCachedScore()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ElectionService(ctx, cache);

        var election = TestHelpers.BuildElection(ElectionStatus.Closed, ElectionType.SingleChoice);
        var candidate = TestHelpers.BuildCandidate(election.Id);
        election.Candidates.Add(candidate);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var first = await sut.GetResultsAsync(election.Id);
        Assert.Equal(0, first.Results.Single().Score);

        ctx.Votes.Add(new Vote
        {
            ElectionId = election.Id,
            CandidateId = candidate.Id,
            VoterEmail = "x@x.com",
            CastAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var second = await sut.GetResultsAsync(election.Id);
        Assert.Equal(0, second.Results.Single().Score);
    }
}
