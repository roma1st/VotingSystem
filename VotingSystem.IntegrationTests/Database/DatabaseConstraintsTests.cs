using System.Net.Http.Json;
using AutoFixture;
using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using VotingSystem.Api.Services;
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

    // Перевіряє обмеження унікальності на рівні реальної БД PostgreSQL.
    // Переконується, що якщо користувач спробує проголосувати двічі за того ж кандидата,
    // база даних відхилить транзакцію з помилкою DbUpdateException.
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

    // Перевіряє запит агрегації результатів на справжньому PostgreSQL (Testcontainers),
    // а не на InMemory. Borda Count: рахуємо суму (N - rank + 1) для RankedChoice.
    [Fact]
    public async Task GetResults_AggregatesVotesCorrectly_OnRealPostgres()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VotingDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IElectionService>();

        var electionId = Guid.NewGuid();
        var c1 = new Candidate { Id = Guid.NewGuid(), ElectionId = electionId, Name = "A", Description = "D", Party = "P" };
        var c2 = new Candidate { Id = Guid.NewGuid(), ElectionId = electionId, Name = "B", Description = "D", Party = "P" };
        var c3 = new Candidate { Id = Guid.NewGuid(), ElectionId = electionId, Name = "C", Description = "D", Party = "P" };

        context.Elections.Add(new Election
        {
            Id = electionId,
            Title = "Aggregation",
            Description = "Result aggregation on real DB",
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            Status = ElectionStatus.Closed,
            Type = ElectionType.SingleChoice
        });
        context.Candidates.AddRange(c1, c2, c3);
        await context.SaveChangesAsync();

        // 5 голосів за c1, 3 за c2, 2 за c3 — всього 10 з відомим розподілом.
        var votes = new List<Vote>();
        for (var i = 0; i < 5; i++)
            votes.Add(new Vote { ElectionId = electionId, CandidateId = c1.Id, VoterEmail = $"u{i}_c1@x.com", CastAt = DateTime.UtcNow });
        for (var i = 0; i < 3; i++)
            votes.Add(new Vote { ElectionId = electionId, CandidateId = c2.Id, VoterEmail = $"u{i}_c2@x.com", CastAt = DateTime.UtcNow });
        for (var i = 0; i < 2; i++)
            votes.Add(new Vote { ElectionId = electionId, CandidateId = c3.Id, VoterEmail = $"u{i}_c3@x.com", CastAt = DateTime.UtcNow });
        context.Votes.AddRange(votes);
        await context.SaveChangesAsync();

        var result = await service.GetResultsAsync(electionId);

        Assert.Equal(5, result.Results.Single(r => r.CandidateId == c1.Id).Score);
        Assert.Equal(3, result.Results.Single(r => r.CandidateId == c2.Id).Score);
        Assert.Equal(2, result.Results.Single(r => r.CandidateId == c3.Id).Score);
        // Відсортовано за спаданням Score: c1 → c2 → c3.
        Assert.Equal(c1.Id, result.Results[0].CandidateId);
        Assert.Equal(c2.Id, result.Results[1].CandidateId);
        Assert.Equal(c3.Id, result.Results[2].CandidateId);
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
        await context.Elections.Where(e => e.Id == electionId).ExecuteDeleteAsync();

        // Ensure candidates and votes are gone
        var isCandidateExists = await context.Candidates.AnyAsync(c => c.Id == candidateId);
        var isVoteExists = await context.Votes.AnyAsync(v => v.ElectionId == electionId);

        Assert.False(isCandidateExists);
        Assert.False(isVoteExists);
    }
}

