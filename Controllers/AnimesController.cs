using MediaManager.Data;
using MediaManager.Models;
using MediaManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaManager.Controllers;

[ApiController]
[Route("api/animes")]
public sealed class AnimesController(
    MediaRepository repository,
    AnimeService service,
    CatalogSnapshotPublisher snapshots) : ControllerBase
{
    [HttpGet]
    public Task<List<Anime>> GetAll() => repository.GetAnimesAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<Anime>> GetById(string id)
    {
        var anime = await repository.GetAnimeAsync(id);
        return anime is null ? NotFound() : Ok(anime);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(Anime request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");
        if (!double.IsFinite(request.Rating) || request.Rating is < 0 or > 5)
            return BadRequest("Rating must be between 0 and 5.");

        var anime = await service.CreateAnimeAsync(
            request.Title, request.Rating, request.Manga, request.AnimeType,
            request.DateWatched, request.Review);

        if (anime is null)
            return NotFound("No matching anime metadata was found.");

        await snapshots.PublishAsync();
        return CreatedAtAction(nameof(GetById), new { id = anime.Id }, anime);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, Anime request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");
        if (!double.IsFinite(request.Rating) || request.Rating is < 0 or > 5)
            return BadRequest("Rating must be between 0 and 5.");

        if (!await repository.UpdateAnimeAsync(id, request))
            return NotFound();

        await snapshots.PublishAsync();
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await repository.DeleteAnimeAsync(id))
            return NotFound();

        await snapshots.PublishAsync();
        return NoContent();
    }
}
