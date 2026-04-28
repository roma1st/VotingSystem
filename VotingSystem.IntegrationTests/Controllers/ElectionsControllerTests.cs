using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.IntegrationTests.Infrastructure;
using Xunit;

namespace VotingSystem.IntegrationTests.Controllers;

public class ElectionsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Fixture _fixture = new();

    public ElectionsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateElection_ReturnsCreatedResponse()
    {
        var request = new CreateElectionDto(
            _fixture.Create<string>(),
            _fixture.Create<string>(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            ElectionType.SingleChoice
        );

        var response = await _client.PostAsJsonAsync("/api/elections", request);

        response.EnsureSuccessStatusCode();
        var returnedElection = await response.Content.ReadFromJsonAsync<ElectionResponseDto>();

        Assert.NotNull(returnedElection);
        Assert.Equal(request.Title, returnedElection.Title);
        Assert.Equal(ElectionStatus.Draft, returnedElection.Status);
    }
}
