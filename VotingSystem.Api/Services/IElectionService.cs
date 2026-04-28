using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;

namespace VotingSystem.Api.Services;

public interface IElectionService
{
    Task<IEnumerable<ElectionResponseDto>> GetElectionsAsync(ElectionStatus? status);
    Task<ElectionResponseDto> CreateElectionAsync(CreateElectionDto request);
    Task<ElectionResponseDto> GetElectionAsync(Guid id);
    Task<CandidateResponseDto> AddCandidateAsync(Guid electionId, CreateCandidateDto request);
    Task CloseElectionAsync(Guid electionId);
    Task VoteAsync(Guid electionId, SubmitVoteDto request);
    Task<ElectionResultDto> GetResultsAsync(Guid electionId);
    Task<TurnoutResponseDto> GetTurnoutAsync(Guid electionId);
}
