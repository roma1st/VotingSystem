using VotingSystem.Api.Domain.Enums;

namespace VotingSystem.Api.DTOs;

public record ElectionResponseDto(
    Guid Id,
    string Title,
    string Description,
    DateTime StartDate,
    DateTime EndDate,
    ElectionStatus Status,
    ElectionType Type,
    IEnumerable<CandidateResponseDto> Candidates
);
