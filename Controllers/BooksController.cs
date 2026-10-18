using MediaManager.Data;
using MediaManager.Models;
using MediaManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaManager.Controllers;

[ApiController]
[Route("api/books")]
public sealed class BooksController(
    BookRepository repository,
    BookService service,
    CatalogSnapshotPublisher snapshots) : ControllerBase
{
    [HttpGet]
    public Task<List<Book>> GetAll() => repository.GetAllAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<Book>> GetById(string id)
    {
        var book = await repository.GetAsync(id);
        return book is null ? NotFound() : Ok(book);
    }

    [HttpGet("{id}/cover")]
    public async Task<IActionResult> GetCover(string id, [FromQuery] string? title = null)
    {
        var cover = await repository.GetCoverAsync(id, title);
        return cover is null ? NotFound() : File(cover.Data, cover.ContentType);
    }

    [Authorize]
    [HttpPost("{id}/cover/refresh")]
    public async Task<IActionResult> RefreshCover(string id, RefreshBookCoverRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Book title is required to search for a cover.");

        if (!await service.RefreshCoverAsync(id, request.Title))
            return NotFound("The cover source did not return a valid image for this book.");

        await snapshots.PublishAsync();
        return Ok(new { coverUpdated = true });
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(Book request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");
        if (!double.IsFinite(request.Rating) || request.Rating is < 0 or > 5)
            return BadRequest("Rating must be between 0 and 5.");

        var book = await service.CreateBookAsync(
            request.Title,
            request.Tags,
            request.Rating,
            request.Physical,
            request.DateRead,
            request.Review);

        if (book is null)
            return NotFound("No matching book metadata was found.");

        await snapshots.PublishAsync();
        return CreatedAtAction(nameof(GetById), new { id = book.Id }, book);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, Book request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");
        if (!double.IsFinite(request.Rating) || request.Rating is < 0 or > 5)
            return BadRequest("Rating must be between 0 and 5.");

        if (!await repository.UpdateAsync(id, request))
            return NotFound();

        await snapshots.PublishAsync();
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await repository.DeleteAsync(id))
            return NotFound();

        await snapshots.PublishAsync();
        return NoContent();
    }
}

public sealed record RefreshBookCoverRequest(string Title);
