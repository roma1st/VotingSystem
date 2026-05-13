using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;

namespace VotingSystem.UnitTests.Services;

public class ElectionService_BusinessRulesTests
{
    // ── Мінімум 2 кандидатів при відкритті виборів ─────────────────────────────

    [Fact]
    public async Task OpenElection_WithZeroCandidates_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Draft);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).OpenElectionAsync(election.Id));
        Assert.Contains("щонайменше 2", ex.Message);
    }

    [Fact]
    public async Task OpenElection_WithOneCandidate_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Draft);
        election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, "Solo"));
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).OpenElectionAsync(election.Id));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task OpenElection_WithTwoOrMoreCandidates_Succeeds(int candidateCount)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Draft);
        for (var i = 0; i < candidateCount; i++)
            election.Candidates.Add(TestHelpers.BuildCandidate(election.Id, $"C{i}"));
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await TestHelpers.CreateService(ctx).OpenElectionAsync(election.Id);

        var updated = await ctx.Elections.FindAsync(election.Id);
        Assert.Equal(ElectionStatus.Active, updated!.Status);
    }

    // ── Вікно голосування: StartDate / EndDate ─────────────────────────────────

    [Fact]
    public async Task Vote_BeforeStartDate_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active);
        election.StartDate = DateTime.UtcNow.AddDays(1);
        election.EndDate = DateTime.UtcNow.AddDays(2);
        var candidate = TestHelpers.BuildCandidate(election.Id);
        election.Candidates.Add(candidate);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("voter@x.com", new() { new VoteItemDto(candidate.Id, null) });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto));
        Assert.Contains("поза", ex.Message);
    }

    [Fact]
    public async Task Vote_AfterEndDate_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active);
        election.StartDate = DateTime.UtcNow.AddDays(-2);
        election.EndDate = DateTime.UtcNow.AddDays(-1);
        var candidate = TestHelpers.BuildCandidate(election.Id);
        election.Candidates.Add(candidate);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("voter@x.com", new() { new VoteItemDto(candidate.Id, null) });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto));
    }

    [Fact]
    public async Task Vote_InsideWindow_Persists()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active);
        election.StartDate = DateTime.UtcNow.AddHours(-1);
        election.EndDate = DateTime.UtcNow.AddHours(1);
        var candidate = TestHelpers.BuildCandidate(election.Id);
        election.Candidates.Add(candidate);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("voter@x.com", new() { new VoteItemDto(candidate.Id, null) });
        await TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto);

        Assert.Equal(1, ctx.Votes.Count(v => v.ElectionId == election.Id));
    }

    [Fact]
    public async Task Vote_RightOnStartDate_Succeeds()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active);
        election.StartDate = DateTime.UtcNow.AddSeconds(-1);
        election.EndDate = DateTime.UtcNow.AddDays(1);
        var candidate = TestHelpers.BuildCandidate(election.Id);
        election.Candidates.Add(candidate);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("boundary@x.com", new() { new VoteItemDto(candidate.Id, null) });
        await TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto);

        Assert.Single(ctx.Votes);
    }

    [Fact]
    public async Task Vote_RightBeforeEndDate_Succeeds()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Active);
        election.StartDate = DateTime.UtcNow.AddDays(-1);
        election.EndDate = DateTime.UtcNow.AddSeconds(5);
        var candidate = TestHelpers.BuildCandidate(election.Id);
        election.Candidates.Add(candidate);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new SubmitVoteDto("boundary@x.com", new() { new VoteItemDto(candidate.Id, null) });
        await TestHelpers.CreateService(ctx).VoteAsync(election.Id, dto);

        Assert.Single(ctx.Votes);
    }
}
