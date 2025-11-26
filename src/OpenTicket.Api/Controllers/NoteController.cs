using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenTicket.Api.Models.Notes;
using OpenTicket.Application.Contracts.Notes.Commands;
using OpenTicket.Application.Contracts.Notes.Queries;
using OpenTicket.Ddd.Application.Cqrs;

namespace OpenTicket.Api.Controllers;

[AllowAnonymous] // TODO: Remove after implementing authentication
public class NoteController : ApiController
{
    private readonly IDispatcher _dispatcher;

    public NoteController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new GetNotesQuery(), ct);
        return Ok(result.Notes);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new GetNoteQuery(id), ct);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNoteRequest request, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new CreateNoteCommand(request.Title, request.Body), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNoteRequest request, CancellationToken ct)
    {
        var success = await _dispatcher.SendAsync(new UpdateNoteCommand(id, request.Title, request.Body), ct);

        if (!success)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var success = await _dispatcher.SendAsync(new RemoveNoteCommand(id), ct);

        if (!success)
            return NotFound();

        return NoContent();
    }
}
