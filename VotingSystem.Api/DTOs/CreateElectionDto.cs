using VotingSystem.Api.Domain.Enums;

namespace VotingSystem.Api.DTOs;

public record CreateElectionDto(
    string Title,
    string Description,
    DateTime StartDate,
    DateTime EndDate,
    ElectionType Type
);
