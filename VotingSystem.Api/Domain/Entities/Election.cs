using VotingSystem.Api.Domain.Enums;

namespace VotingSystem.Api.Domain.Entities;

public class Election
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public ElectionStatus Status { get; set; } = ElectionStatus.Draft;
    public ElectionType Type { get; set; }

    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
