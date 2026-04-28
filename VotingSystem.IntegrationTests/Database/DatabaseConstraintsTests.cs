using System.Net.Http.Json;
using AutoFixture;
using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using VotingSystem.IntegrationTests.Infrastructure;

namespace VotingSystem.IntegrationTests.Database;

public class DatabaseConstraintsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly Fixture _fixture = new();

    public DatabaseConstraintsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Vote_UniqueIndex_ShouldPreventMultipleVotesBySameUserForSaveGivenElectionAndCandidate()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VotingDbContext>();

        var electionId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        
        var election = new Election { Id = electionId, Title = "Test", Description = "Test", Status = ElectionStatus.Active, Type = ElectionType.SingleChoice };
        var candidate = new Candidate { Id = candidateId, ElectionId = electionId, Name = "Name", Description = "Desc", Party = "Party" };

        context.Elections.Add(election);
        context.Candidates.Add(candidate);
        await context.SaveChangesAsync();

        var vote1 = new Vote { ElectionId = electionId, CandidateId = candidateId, VoterEmail = "duplicate@test.com", CastAt = DateTime.UtcNow };
        var vote2 = new Vote { ElectionId = electionId, CandidateId = candidateId, VoterEmail = "duplicate@test.com", CastAt = DateTime.UtcNow };

        context.Votes.Add(vote1);
        await context.SaveChangesAsync();

        context.Votes.Add(vote2);
        
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("duplicate key value violates unique constraint", ex.InnerException?.Message ?? "");
    }

    [Fact]
    public async Task ElectionDelete_ShouldCascadeDeleteCandidatesAndVotes()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VotingDbContext>();

        var electionId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();

        var election = new Election { Id = electionId, Title = "Cascade", Description = "Test", Status = ElectionStatus.Draft, Type = ElectionType.SingleChoice };
        var candidate = new Candidate { Id = candidateId, ElectionId = electionId, Name = "Name", Description = "Desc", Party = "Party" };

        context.Elections.Add(election);
        context.Candidates.Add(candidate);
        await context.SaveChangesAsync();

        context.Votes.Add(new Vote { ElectionId = electionId, CandidateId = candidateId, VoterEmail = "voter@test.com" });
        await context.SaveChangesAsync();

        // Delete election
        context.Elections.Remove(election);
        await context.SaveChangesAsync();

        // Ensure candidates and votes are gone
        var isCandidateExists = await context.Candidates.AnyAsync(c => c.Id == candidateId);
        var isVoteExists = await context.Votes.AnyAsync(v => v.ElectionId == electionId);

        Assert.False(isCandidateExists);
        Assert.False(isVoteExists);
    }
}
