using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VotingSystem.Api.Domain.Entities;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.Api.Infrastructure.Data;

namespace VotingSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ElectionsController : ControllerBase
{
    private readonly VotingDbContext _context;

    public ElectionsController(VotingDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetElections([FromQuery] ElectionStatus? status)
    {
        var query = _context.Elections.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status);
        }

        var elections = await query
            .Select(e => new ElectionResponseDto(
                e.Id, e.Title, e.Description, e.StartDate, e.EndDate, e.Status, e.Type,
                e.Candidates.Select(c => new CandidateResponseDto(c.Id, c.Name, c.Description, c.Party, c.PhotoUrl))))
            .ToListAsync();

        return Ok(elections);
    }

    [HttpPost]
    public async Task<IActionResult> CreateElection([FromBody] CreateElectionDto request)
    {
        var election = new Election
        {
            Title = request.Title,
            Description = request.Description,
            StartDate = request.StartDate.ToUniversalTime(),
            EndDate = request.EndDate.ToUniversalTime(),
            Type = request.Type,
            Status = ElectionStatus.Draft
        };

        _context.Elections.Add(election);
        await _context.SaveChangesAsync();

        var response = new ElectionResponseDto(
            election.Id, election.Title, election.Description, election.StartDate, election.EndDate, election.Status, election.Type, 
            new List<CandidateResponseDto>());

        return CreatedAtAction(nameof(GetElection), new { id = election.Id }, response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetElection(Guid id)
    {
        var election = await _context.Elections
            .Include(e => e.Candidates)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (election == null) return NotFound();

        var response = new ElectionResponseDto(
            election.Id, election.Title, election.Description, election.StartDate, election.EndDate, election.Status, election.Type,
            election.Candidates.Select(c => new CandidateResponseDto(c.Id, c.Name, c.Description, c.Party, c.PhotoUrl)));

        return Ok(response);
    }

    [HttpPost("{id}/candidates")]
    public async Task<IActionResult> AddCandidate(Guid id, [FromBody] CreateCandidateDto request)
    {
        var election = await _context.Elections.FindAsync(id);
        if (election == null) return NotFound();

        if (election.Status != ElectionStatus.Draft)
            return BadRequest("Кандидатів можна додавати лише до виборів у статусі Draft.");

        var candidate = new Candidate
        {
            ElectionId = id,
            Name = request.Name,
            Description = request.Description,
            Party = request.Party,
            PhotoUrl = request.PhotoUrl
        };

        _context.Candidates.Add(candidate);
        await _context.SaveChangesAsync();

        var response = new CandidateResponseDto(candidate.Id, candidate.Name, candidate.Description, candidate.Party, candidate.PhotoUrl);
        return Ok(response);
    }

    [HttpPatch("{id}/close")]
    public async Task<IActionResult> CloseElection(Guid id)
    {
        var election = await _context.Elections.FindAsync(id);
        if (election == null) return NotFound();

        if (election.Status == ElectionStatus.Closed)
            return BadRequest("Вибори вже закриті.");

        election.Status = ElectionStatus.Closed;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id}/vote")]
    public async Task<IActionResult> Vote(Guid id, [FromBody] SubmitVoteDto request)
    {
        var election = await _context.Elections.Include(e => e.Candidates).FirstOrDefaultAsync(e => e.Id == id);
        if (election == null) return NotFound();

        if (election.Status != ElectionStatus.Active)
            return BadRequest("Голосувати можна тільки під час активного періоду виборів.");

        var hasVoted = await _context.Votes.AnyAsync(v => v.ElectionId == id && v.VoterEmail == request.VoterEmail);
        if (hasVoted)
            return BadRequest("Ви вже проголосували на цих виборах.");

        if (election.Type == ElectionType.SingleChoice)
        {
            if (request.Votes.Count != 1) return BadRequest("Для цих виборів потрібно обрати рівно одного кандидата.");
            var candidateId = request.Votes.First().CandidateId;
            if (!election.Candidates.Any(c => c.Id == candidateId)) return BadRequest("Кандидата не знайдено.");
            
            _context.Votes.Add(new Vote { ElectionId = id, VoterEmail = request.VoterEmail, CandidateId = candidateId, CastAt = DateTime.UtcNow });
        }
        else if (election.Type == ElectionType.RankedChoice)
        {
            var candidateIds = election.Candidates.Select(c => c.Id).ToList();
            if (request.Votes.Count != candidateIds.Count) return BadRequest("Потрібно проранжувати всіх кандидатів.");
            
            var submittedCandidateIds = request.Votes.Select(v => v.CandidateId).ToList();
            if (submittedCandidateIds.Distinct().Count() != candidateIds.Count || !submittedCandidateIds.All(cid => candidateIds.Contains(cid)))
                return BadRequest("Неправильні кандидати для ранжування.");

            var allRanks = request.Votes.Select(v => v.Rank ?? 0).ToList();
            if (allRanks.Distinct().Count() != candidateIds.Count || allRanks.Any(r => r <= 0 || r > candidateIds.Count))
                return BadRequest("Недійсні ранги: ранги повинні бути унікальними та від 1 до N.");

            foreach (var voteItem in request.Votes)
            {
                _context.Votes.Add(new Vote { ElectionId = id, VoterEmail = request.VoterEmail, CandidateId = voteItem.CandidateId, Rank = voteItem.Rank.Value, CastAt = DateTime.UtcNow });
            }
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("{id}/results")]
    public async Task<IActionResult> GetResults(Guid id)
    {
        var election = await _context.Elections.Include(e => e.Candidates).FirstOrDefaultAsync(e => e.Id == id);
        if (election == null) return NotFound();

        if (election.Status != ElectionStatus.Closed)
            return BadRequest("Результати видимі тільки після закриття виборів.");

        var votes = await _context.Votes.Where(v => v.ElectionId == id).ToListAsync();
        var results = new List<CandidateResultDto>();

        if (election.Type == ElectionType.SingleChoice)
        {
            var grouped = votes.GroupBy(v => v.CandidateId).ToDictionary(g => g.Key, g => g.Count());
            results = election.Candidates.Select(c => new CandidateResultDto(c.Id, c.Name, grouped.GetValueOrDefault(c.Id, 0))).ToList();
        }
        else if (election.Type == ElectionType.RankedChoice)
        {
            // Використовуємо Borda Count для простоти розрахунків
            var N = election.Candidates.Count;
            var grouped = votes.GroupBy(v => v.CandidateId).ToDictionary(g => g.Key, g => g.Sum(v => N - (v.Rank ?? 0) + 1));
            results = election.Candidates.Select(c => new CandidateResultDto(c.Id, c.Name, grouped.GetValueOrDefault(c.Id, 0))).ToList();
        }

        return Ok(new ElectionResultDto(id, results.OrderByDescending(r => r.Score).ToList()));
    }

    [HttpGet("{id}/turnout")]
    public async Task<IActionResult> GetTurnout(Guid id)
    {
        var election = await _context.Elections.FindAsync(id);
        if (election == null) return NotFound();

        var totalVoters = await _context.Votes
            .Where(v => v.ElectionId == id)
            .Select(v => v.VoterEmail)
            .Distinct()
            .CountAsync();

        return Ok(new TurnoutResponseDto(id, totalVoters));
    }
}
