namespace VotingSystem.Api.Domain.Entities;

public class Candidate
{
    public Guid Id { get; set; }
    public Guid ElectionId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Party { get; set; }
    public string? PhotoUrl { get; set; }

    public Election? Election { get; set; }
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
