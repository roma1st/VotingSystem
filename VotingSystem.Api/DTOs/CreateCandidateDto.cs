namespace VotingSystem.Api.DTOs;

public record CreateCandidateDto(
    string Name,
    string Description,
    string Party,
    string? PhotoUrl
);
