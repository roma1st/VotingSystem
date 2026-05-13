using Microsoft.EntityFrameworkCore;
using VotingSystem.Api.Domain.Enums;

namespace VotingSystem.UnitTests.Services;

public class ElectionService_LifecycleTests
{
    [Fact]
    public async Task OpenElection_FromDraft_ChangesStatusToActive()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Draft);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await TestHelpers.CreateService(ctx).OpenElectionAsync(election.Id);

        var updated = await ctx.Elections.FindAsync(election.Id);
        Assert.Equal(ElectionStatus.Active, updated!.Status);
    }

    [Theory]
    [InlineData(ElectionStatus.Active)]
    [InlineData(ElectionStatus.Closed)]
    public async Task OpenElection_NotDraft_Throws(ElectionStatus status)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(status);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).OpenElectionAsync(election.Id));
    }

    [Fact]
    public async Task OpenElection_NonExistent_ThrowsKeyNotFound()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => TestHelpers.CreateService(ctx).OpenElectionAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(ElectionStatus.Draft)]
    [InlineData(ElectionStatus.Active)]
    public async Task CloseElection_NotClosed_ChangesStatusToClosed(ElectionStatus status)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(status);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await TestHelpers.CreateService(ctx).CloseElectionAsync(election.Id);

        var updated = await ctx.Elections.FindAsync(election.Id);
        Assert.Equal(ElectionStatus.Closed, updated!.Status);
    }

    [Fact]
    public async Task CloseElection_AlreadyClosed_Throws()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Closed);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).CloseElectionAsync(election.Id));
    }

    [Fact]
    public async Task CloseElection_NonExistent_ThrowsKeyNotFound()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => TestHelpers.CreateService(ctx).CloseElectionAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task AddCandidate_DraftElection_PersistsCandidate()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(ElectionStatus.Draft);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new VotingSystem.Api.DTOs.CreateCandidateDto("Name", "Desc", "Party", null);
        var result = await TestHelpers.CreateService(ctx).AddCandidateAsync(election.Id, dto);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(1, ctx.Candidates.Count(c => c.ElectionId == election.Id));
    }

    [Theory]
    [InlineData(ElectionStatus.Active)]
    [InlineData(ElectionStatus.Closed)]
    public async Task AddCandidate_NotDraft_Throws(ElectionStatus status)
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var election = TestHelpers.BuildElection(status);
        ctx.Elections.Add(election);
        await ctx.SaveChangesAsync();

        var dto = new VotingSystem.Api.DTOs.CreateCandidateDto("N", "D", "P", null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestHelpers.CreateService(ctx).AddCandidateAsync(election.Id, dto));
    }

    [Fact]
    public async Task AddCandidate_NonExistentElection_ThrowsKeyNotFound()
    {
        using var ctx = TestHelpers.CreateInMemoryContext();
        var dto = new VotingSystem.Api.DTOs.CreateCandidateDto("N", "D", "P", null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => TestHelpers.CreateService(ctx).AddCandidateAsync(Guid.NewGuid(), dto));
    }
}
