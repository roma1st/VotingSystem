namespace VotingSystem.Api.Domain.Entities;

public class Vote
{
    public Guid Id { get; set; }
    public Guid ElectionId { get; set; }
    public required string VoterEmail { get; set; }
    public Guid CandidateId { get; set; }
    public int? Rank { get; set; }
    public DateTime CastAt { get; set; }

    public Election? Election { get; set; }
    public Candidate? Candidate { get; set; }
}
