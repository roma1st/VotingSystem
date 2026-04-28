using Microsoft.AspNetCore.Mvc;
using VotingSystem.Api.Domain.Enums;
using VotingSystem.Api.DTOs;
using VotingSystem.Api.Services;

namespace VotingSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ElectionsController : ControllerBase
{
    private readonly IElectionService _electionService;

    public ElectionsController(IElectionService electionService)
    {
        _electionService = electionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetElections([FromQuery] ElectionStatus? status)
    {
        var elections = await _electionService.GetElectionsAsync(status);
        return Ok(elections);
    }

    [HttpPost]
    public async Task<IActionResult> CreateElection([FromBody] CreateElectionDto request)
    {
        var response = await _electionService.CreateElectionAsync(request);
        return CreatedAtAction(nameof(GetElection), new { id = response.Id }, response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetElection(Guid id)
    {
        try
        {
            var response = await _electionService.GetElectionAsync(id);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/candidates")]
    public async Task<IActionResult> AddCandidate(Guid id, [FromBody] CreateCandidateDto request)
    {
        try
        {
            var response = await _electionService.AddCandidateAsync(id, request);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{id}/close")]
    public async Task<IActionResult> CloseElection(Guid id)
    {
        try
        {
            await _electionService.CloseElectionAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/vote")]
    public async Task<IActionResult> Vote(Guid id, [FromBody] SubmitVoteDto request)
    {
        try
        {
            await _electionService.VoteAsync(id, request);
            return Ok();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}/results")]
    public async Task<IActionResult> GetResults(Guid id)
    {
        try
        {
            var response = await _electionService.GetResultsAsync(id);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}/turnout")]
    public async Task<IActionResult> GetTurnout(Guid id)
    {
        try
        {
            var response = await _electionService.GetTurnoutAsync(id);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
