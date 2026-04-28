using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using VotingSystem.Api.Infrastructure.Data;
using VotingSystem.Api.Domain.Entities;
using Xunit;

namespace VotingSystem.IntegrationTests.Integration;

public class LifecycleTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly Fixture _fixture = new();

    public LifecycleTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullVotingLifecycle_ShouldSucceed()
    {
        // 1. Create Election (Status Draft)
        var createElectionDto = new CreateElectionDto("Presidential", "Decide the president", DateTime.UtcNow, DateTime.UtcNow.AddDays(5), ElectionType.SingleChoice);
        var r1 = await _client.PostAsJsonAsync("/api/elections", createElectionDto);
        r1.EnsureSuccessStatusCode();
        var election = await r1.Content.ReadFromJsonAsync<ElectionResponseDto>();

        Assert.NotNull(election);
        var electionId = election.Id;

        // 2. Add Candidates
        var c1 = new CreateCandidateDto("Alice", "D", "P", null);
        var c2 = new CreateCandidateDto("Bob", "D", "P", null);

        var rC1 = await _client.PostAsJsonAsync($"/api/elections/{electionId}/candidates", c1);
        rC1.EnsureSuccessStatusCode();
        var candidate1 = await rC1.Content.ReadFromJsonAsync<CandidateResponseDto>();

        var rC2 = await _client.PostAsJsonAsync($"/api/elections/{electionId}/candidates", c2);
        var candidate2 = await rC2.Content.ReadFromJsonAsync<CandidateResponseDto>();

        // 3. Try voting during Draft (Should fail)
        var voteRequest = new SubmitVoteDto("voter@test.com", new List<VoteItemDto> { new(candidate1!.Id, null) });
        var rVoteFail = await _client.PostAsJsonAsync($"/api/elections/{electionId}/vote", voteRequest);
        Assert.Equal(HttpStatusCode.BadRequest, rVoteFail.StatusCode); 

        // 4. Force Active via DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VotingDbContext>();
            var dbElection = await db.Elections.FindAsync(electionId);
            if (dbElection != null)
            {
                dbElection.Status = ElectionStatus.Active;
                await db.SaveChangesAsync();
            }
        }

        // 5. Vote Successfully
        var rVoteSuccess = await _client.PostAsJsonAsync($"/api/elections/{electionId}/vote", voteRequest);
        rVoteSuccess.EnsureSuccessStatusCode();

        // 6. Try checking results before closing (Should fail)
        var rResFail = await _client.GetAsync($"/api/elections/{electionId}/results");
        Assert.Equal(HttpStatusCode.BadRequest, rResFail.StatusCode);

        // 7. Close Election
        var rClose = await _client.PatchAsync($"/api/elections/{electionId}/close", null);
        rClose.EnsureSuccessStatusCode();

        // 8. Check Results Successfully
        var rResSuccess = await _client.GetAsync($"/api/elections/{electionId}/results");
        rResSuccess.EnsureSuccessStatusCode();

        var results = await rResSuccess.Content.ReadFromJsonAsync<ElectionResultDto>();
        Assert.NotNull(results);
        Assert.Equal(candidate1.Id, results.Results.First().CandidateId);
        Assert.Equal(1, results.Results.First().Score);
    }
}
