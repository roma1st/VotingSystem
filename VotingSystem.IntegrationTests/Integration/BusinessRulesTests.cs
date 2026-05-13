using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.Api.Infrastructure.Data;
using VotingSystem.IntegrationTests.Infrastructure;
using Xunit;

namespace VotingSystem.IntegrationTests.Integration;

public class BusinessRulesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public BusinessRulesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenElection_WithSingleCandidate_Returns400()
    {
        var createDto = new CreateElectionDto(
            "Min candidates check",
            "Should not open with one candidate",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            ElectionType.SingleChoice);

        var rCreate = await _client.PostAsJsonAsync("/api/elections", createDto);
        rCreate.EnsureSuccessStatusCode();
        var election = await rCreate.Content.ReadFromJsonAsync<ElectionResponseDto>();
        Assert.NotNull(election);

        // Додаємо тільки одного кандидата
        var candDto = new CreateCandidateDto("Solo", "D", "P", null);
        var rCand = await _client.PostAsJsonAsync($"/api/elections/{election.Id}/candidates", candDto);
        rCand.EnsureSuccessStatusCode();

        var rOpen = await _client.PostAsync($"/api/elections/{election.Id}/open", null);

        Assert.Equal(HttpStatusCode.BadRequest, rOpen.StatusCode);
    }

    [Fact]
    public async Task Vote_OutsideDateWindow_Returns400()
    {
        // Створюємо вибори з вікном у минулому. Адмін відкриває їх все одно (з 2 кандидатами).
        // Бізнес-правило має заблокувати голос, бо UtcNow > EndDate.
        var electionId = Guid.NewGuid();
        Guid candidate1Id;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VotingDbContext>();

            var election = new Election
            {
                Id = electionId,
                Title = "Expired window",
                Description = "EndDate in the past",
                StartDate = DateTime.UtcNow.AddDays(-5),
                EndDate = DateTime.UtcNow.AddDays(-1),
                Status = ElectionStatus.Active,
                Type = ElectionType.SingleChoice
            };
            var c1 = new Candidate { Id = Guid.NewGuid(), ElectionId = electionId, Name = "A", Description = "D", Party = "P" };
            var c2 = new Candidate { Id = Guid.NewGuid(), ElectionId = electionId, Name = "B", Description = "D", Party = "P" };
            candidate1Id = c1.Id;

            db.Elections.Add(election);
            db.Candidates.AddRange(c1, c2);
            await db.SaveChangesAsync();
        }

        var voteDto = new SubmitVoteDto("voter@x.com", new() { new VoteItemDto(candidate1Id, null) });
        var rVote = await _client.PostAsJsonAsync($"/api/elections/{electionId}/vote", voteDto);

        Assert.Equal(HttpStatusCode.BadRequest, rVote.StatusCode);
    }
}
