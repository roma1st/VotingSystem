namespace VotingSystem.Api.DTOs;

public record VoteItemDto(
    Guid CandidateId,
    int? Rank
);

public record SubmitVoteDto(
    string VoterEmail,
    List<VoteItemDto> Votes
);
