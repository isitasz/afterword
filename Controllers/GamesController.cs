using MediaManager.Data;
using MediaManager.Models;
using MediaManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaManager.Controllers;

[ApiController]
[Route("api/games")]
public sealed class GamesController(
    MediaRepository repository,
    GameService service,
    CatalogSnapshotPublisher snapshots) : ControllerBase
{
    [HttpGet]
    public Task<List<Game>> GetAll() => repository.GetGamesAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<Game>> GetById(string id)
    {
        var game = await repository.GetGameAsync(id);
        return game is null ? NotFound() : Ok(game);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(Game request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");
        if (!double.IsFinite(request.Rating) || request.Rating is < 0 or > 5)
            return BadRequest("Rating must be between 0 and 5.");

        var game = await service.CreateGameAsync(
            request.Title, request.Hours, request.Rating, request.Owned,
            request.DatePlayed, request.Review);

        if (game is null)
            return NotFound("No matching game metadata was found.");

        await snapshots.PublishAsync();
        return CreatedAtAction(nameof(GetById), new { id = game.Id }, game);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, Game request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");
        if (!double.IsFinite(request.Rating) || request.Rating is < 0 or > 5)
            return BadRequest("Rating must be between 0 and 5.");

        if (!await repository.UpdateGameAsync(id, request))
            return NotFound();

        await snapshots.PublishAsync();
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await repository.DeleteGameAsync(id))
            return NotFound();

        await snapshots.PublishAsync();
        return NoContent();
    }
}
