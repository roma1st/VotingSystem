namespace VotingSystem.Api.DTOs;

public record CandidateResultDto(
    Guid CandidateId,
    string CandidateName,
    int Score
);

public record ElectionResultDto(
    Guid ElectionId,
    List<CandidateResultDto> Results
);

public record TurnoutResponseDto(
    Guid ElectionId,
    int TotalVoters
);
