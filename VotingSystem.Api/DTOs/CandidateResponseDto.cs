namespace VotingSystem.Api.DTOs;

public record CandidateResponseDto(
    Guid Id,
    string Name,
    string Description,
    string Party,
    string? PhotoUrl
);
