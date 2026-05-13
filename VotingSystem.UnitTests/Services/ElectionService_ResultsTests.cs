using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;

namespace VotingSystem.UnitTests.Services;

public class ElectionService_ResultsTests
{
    [Fact]
    public async Task GetResults_NonExistent_ThrowsKeyNotFound()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => TestHelpers.CreateService(ctx).GetResultsAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(ElectionStatus.Draft)]
    [InlineData(ElectionStatus.Active)]
    public async Task GetResults_NotClosed_Throws(ElectionStatus status)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(status);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).GetResultsAsync(election.Id));
    }

    [Fact]
    public async Task GetResults_SingleChoice_CountsVotesPerCandidate()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Closed, ElectionType.SingleChoice);
        var c1 = TestHelpers.BuildCandidate(election.Id, "C1");
        var c2 = TestHelpers.BuildCandidate(election.Id, "C2");
        election.Candidates.Add(c1);
        election.Candidates.Add(c2);
        ctx.Elections.Add(election);

        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c1.Id, VoterEmail = "a@x.com", CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c2.Id, VoterEmail = "b@x.com", CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c2.Id, VoterEmail = "c@x.com", CastAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateService(ctx).GetResultsAsync(election.Id);

        Assert.Equal(2, result.Results.Single(r => r.CandidateId == c2.Id).Score);
        Assert.Equal(1, result.Results.Single(r => r.CandidateId == c1.Id).Score);
    }

    [Fact]
    public async Task GetResults_OrdersByScoreDescending()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Closed, ElectionType.SingleChoice);
        var c1 = TestHelpers.BuildCandidate(election.Id, "C1");
        var c2 = TestHelpers.BuildCandidate(election.Id, "C2");
        election.Candidates.Add(c1);
        election.Candidates.Add(c2);
        ctx.Elections.Add(election);

        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c2.Id, VoterEmail = "a@x.com", CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c2.Id, VoterEmail = "b@x.com", CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c1.Id, VoterEmail = "c@x.com", CastAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateService(ctx).GetResultsAsync(election.Id);

        Assert.Equal(c2.Id, result.Results.First().CandidateId);
    }

    [Fact]
    public async Task GetResults_RankedChoice_AppliesBordaCount()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Closed, ElectionType.RankedChoice);
        var c1 = TestHelpers.BuildCandidate(election.Id, "C1");
        var c2 = TestHelpers.BuildCandidate(election.Id, "C2");
        var c3 = TestHelpers.BuildCandidate(election.Id, "C3");
        election.Candidates.Add(c1);
        election.Candidates.Add(c2);
        election.Candidates.Add(c3);
        ctx.Elections.Add(election);

        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c1.Id, VoterEmail = "v@x.com", Rank = 1, CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c2.Id, VoterEmail = "v@x.com", Rank = 2, CastAt = DateTime.UtcNow });
        ctx.Votes.Add(new Vote { ElectionId = election.Id, CandidateId = c3.Id, VoterEmail = "v@x.com", Rank = 3, CastAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateService(ctx).GetResultsAsync(election.Id);

        Assert.Equal(3, result.Results.Single(r => r.CandidateId == c1.Id).Score);
        Assert.Equal(2, result.Results.Single(r => r.CandidateId == c2.Id).Score);
        Assert.Equal(1, result.Results.Single(r => r.CandidateId == c3.Id).Score);
    }

    [Fact]
    public async Task GetResults_NoVotes_ReturnsZeroScoreForAllCandidates()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Closed, ElectionType.SingleChoice);
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "C1"));
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "C2"));
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var result = await TestHelpers.CreateService(ctx).GetResultsAsync(election.Id);

        Assert.All(result.Results, r => Assert.Equal(0, r.Score));
    }
}
