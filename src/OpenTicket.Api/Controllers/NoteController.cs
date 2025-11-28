using Microsoft.AspNetCore.Mvc;
using OpenTicket.Api.Models.Notes;
using OpenTicket.Application.Contracts.Notes.Commands;
using OpenTicket.Application.Contracts.Notes.Dtos;
using OpenTicket.Application.Contracts.Notes.Queries;
using OpenTicket.Ddd.Application.Cqrs;

namespace OpenTicket.Api.Controllers;

/// <summary>
/// Manages notes - a simple CQRS example.
/// </summary>
[Produces("application/json")]
public class NoteController : ApiController
{
    private readonly IDispatcher _dispatcher;

    public NoteController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Get all notes.
    /// </summary>
    /// <returns>List of all notes.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NoteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new GetNotesQuery(), ct);
        return Ok(result.Notes);
    }

    /// <summary>
    /// Get a note by ID.
    /// </summary>
    /// <param name="id">The note ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The note if found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.QueryAsync(new GetNoteQuery(id), ct);

        return result.Match(
            note => Ok(note),
            errors => Problem(errors));
    }

    /// <summary>
    /// Create a new note.
    /// Non-subscribers are limited to 3 notes per day.
    /// </summary>
    /// <param name="request">The note creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created note ID.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CreateNoteCommandResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateNoteRequest request, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new CreateNoteCommand(request.Title, request.Body), ct);

        return result.Match(
            success => CreatedAtAction(nameof(GetById), new { id = success.Id }, success),
            errors => Problem(errors));
    }

    /// <summary>
    /// Update an existing note (full replacement).
    /// Only the creator or admin can update.
    /// </summary>
    /// <param name="id">The note ID.</param>
    /// <param name="request">The note update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content if successful.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNoteRequest request, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new UpdateNoteCommand(id, request.Title, request.Body), ct);

        return result.Match(
            _ => NoContent(),
            errors => Problem(errors));
    }

    /// <summary>
    /// Partially update a note.
    /// Only provided fields will be updated.
    /// Only the creator or admin can update.
    /// </summary>
    /// <param name="id">The note ID.</param>
    /// <param name="request">The partial update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content if successful.</returns>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Patch(Guid id, [FromBody] PatchNoteRequest request, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new PatchNoteCommand(id, request.Title, request.Body), ct);

        return result.Match(
            _ => NoContent(),
            errors => Problem(errors));
    }

    /// <summary>
    /// Delete a note.
    /// Only the creator or admin can delete.
    /// </summary>
    /// <param name="id">The note ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content if successful.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _dispatcher.SendAsync(new RemoveNoteCommand(id), ct);

        return result.Match(
            _ => NoContent(),
            errors => Problem(errors));
    }
}
