using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VotingSystem.Api.Controllers;
using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.Api.Infrastructure.Data;
using Xunit;

namespace VotingSystem.UnitTests.Controllers;

public class VotingLogicUnitTests
{
    private DbContextOptions<VotingDbContext> _options;

    public VotingLogicUnitTests()
    {
        _options = new DbContextOptionsBuilder<VotingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task Vote_SingleChoice_ShouldCalculateCorrectly()
    {
        using var context = new VotingDbContext(_options);
        var controller = new ElectionsController(context);

        var electionId = Guid.NewGuid();
        var candidate1 = new Candidate { Id = Guid.NewGuid(), ElectionId = electionId, Name = "C1", Description = "D1", Party = "P1" };
        var candidate2 = new Candidate { Id = Guid.NewGuid(), ElectionId = electionId, Name = "C2", Description = "D2", Party = "P2" };

        context.Elections.Add(new Election
        {
            Id = electionId, Title = "Title", Description = "Desc", 
            Status = ElectionStatus.Active, Type = ElectionType.SingleChoice,
            Candidates = new List<Candidate> { candidate1, candidate2 }
        });
        await context.SaveChangesAsync();

        // 1 User votes for Candidate 1
        await controller.Vote(electionId, new SubmitVoteDto("voter1@test.com", new List<VoteItemDto> { new(candidate1.Id, null) }));
        
        // 2 Users vote for Candidate 2
        await controller.Vote(electionId, new SubmitVoteDto("voter2@test.com", new List<VoteItemDto> { new(candidate2.Id, null) }));
        await controller.Vote(electionId, new SubmitVoteDto("voter3@test.com", new List<VoteItemDto> { new(candidate2.Id, null) }));

        // Close election to view results
        await controller.CloseElection(electionId);

        var result = await controller.GetResults(electionId) as OkObjectResult;
        var resultDto = result?.Value as ElectionResultDto;

        Assert.NotNull(resultDto);
        Assert.Equal(2, resultDto.Results.Find(r => r.CandidateId == candidate2.Id)?.Score);
        Assert.Equal(1, resultDto.Results.Find(r => r.CandidateId == candidate1.Id)?.Score);
    }

    [Fact]
    public async Task Vote_RankedChoice_ShouldCalculateByBordaCount()
    {
        using var context = new VotingDbContext(_options);
        var controller = new ElectionsController(context);

        var electionId = Guid.NewGuid();
        var candidate1 = new Candidate { Id = Guid.NewGuid(), ElectionId = electionId, Name = "C1", Description = "D", Party = "P" };
        var candidate2 = new Candidate { Id = Guid.NewGuid(), ElectionId = electionId, Name = "C2", Description = "D", Party = "P" };
        var candidate3 = new Candidate { Id = Guid.NewGuid(), ElectionId = electionId, Name = "C3", Description = "D", Party = "P" };

        context.Elections.Add(new Election
        {
            Id = electionId, Title = "Title", Description = "Desc", 
            Status = ElectionStatus.Active, Type = ElectionType.RankedChoice,
            Candidates = new List<Candidate> { candidate1, candidate2, candidate3 }
        });
        await context.SaveChangesAsync();

        var votes = new List<VoteItemDto>
        {
            new(candidate1.Id, 1), // N-Rank+1 = 3 - 1 + 1 = 3 points
            new(candidate2.Id, 2), // 3 - 2 + 1 = 2 points
            new(candidate3.Id, 3)  // 3 - 3 + 1 = 1 point
        };

        var response = await controller.Vote(electionId, new SubmitVoteDto("voter1@test.com", votes));
        Assert.IsType<OkResult>(response);

        await controller.CloseElection(electionId);

        var getResult = await controller.GetResults(electionId) as OkObjectResult;
        var dto = getResult?.Value as ElectionResultDto;

        Assert.NotNull(dto);
        Assert.Equal(3, dto.Results.Find(c => c.CandidateId == candidate1.Id)?.Score);
        Assert.Equal(2, dto.Results.Find(c => c.CandidateId == candidate2.Id)?.Score);
        Assert.Equal(1, dto.Results.Find(c => c.CandidateId == candidate3.Id)?.Score);
    }

    [Fact]
    public async Task GetTurnout_ShouldReturnUniqueVotersCount()
    {
        using var context = new VotingDbContext(_options);
        var controller = new ElectionsController(context);

        var electionId = Guid.NewGuid();
        context.Elections.Add(new Election { Id = electionId, Title = "T", Description = "D", Status = ElectionStatus.Active, Type = ElectionType.SingleChoice });
        context.Candidates.Add(new Candidate { Id = Guid.NewGuid(), ElectionId = electionId, Name = "C", Description="D", Party="P" });
        
        // Single user voting twice shouldn't theoretically happen (caught by another validation), but the DB uniqueness for test is just counted by distinct VoterEmail.
        context.Votes.Add(new Vote { Id = Guid.NewGuid(), ElectionId = electionId, CandidateId = Guid.NewGuid(), VoterEmail = "user1@test.com", CastAt = DateTime.UtcNow });
        context.Votes.Add(new Vote { Id = Guid.NewGuid(), ElectionId = electionId, CandidateId = Guid.NewGuid(), VoterEmail = "user2@test.com", CastAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var result = await controller.GetTurnout(electionId) as OkObjectResult;
        var dto = result?.Value as TurnoutResponseDto;

        Assert.NotNull(dto);
        Assert.Equal(2, dto.TotalVoters);
    }
}
