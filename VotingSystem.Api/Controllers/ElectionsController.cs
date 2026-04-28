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
}
