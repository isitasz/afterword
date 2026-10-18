using MediaManager.Data;
using MediaManager.Models;
using MediaManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaManager.Controllers;

[ApiController]
[Route("api/movies")]
public sealed class MoviesController(
    MediaRepository repository,
    MovieService service,
    CatalogSnapshotPublisher snapshots) : ControllerBase
{
    [HttpGet]
    public Task<List<Movie>> GetAll() => repository.GetMoviesAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<Movie>> GetById(string id)
    {
        var movie = await repository.GetMovieAsync(id);
        return movie is null ? NotFound() : Ok(movie);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(Movie request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");
        if (!double.IsFinite(request.Rating) || request.Rating is < 0 or > 5)
            return BadRequest("Rating must be between 0 and 5.");

        var movie = await service.CreateMovieAsync(
            request.Title, request.Rating, request.Cinema,
            request.DateWatched, request.Review);

        if (movie is null)
            return NotFound("No matching movie metadata was found.");

        await snapshots.PublishAsync();
        return CreatedAtAction(nameof(GetById), new { id = movie.Id }, movie);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, Movie request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("Title is required.");
        if (!double.IsFinite(request.Rating) || request.Rating is < 0 or > 5)
            return BadRequest("Rating must be between 0 and 5.");

        if (!await repository.UpdateMovieAsync(id, request))
            return NotFound();

        await snapshots.PublishAsync();
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await repository.DeleteMovieAsync(id))
            return NotFound();

        await snapshots.PublishAsync();
        return NoContent();
    }
}
