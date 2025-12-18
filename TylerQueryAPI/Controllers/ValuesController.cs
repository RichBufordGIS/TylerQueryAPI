using Microsoft.AspNetCore.Mvc;
using TylerInfoAPI.Models;
using TylerInfoAPI.Services;

namespace TylerInfoAPI.Controllers;

[ApiController]
[Route("api/tyler")]
public sealed class TylerController : ControllerBase
{
    private readonly ITylerService _svc;
    private readonly ITcmService _tcm;

    public TylerController(ITylerService svc, ITcmService tcm)
    {
        _svc = svc;
        _tcm = tcm;
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok("Tyler API is running");

    // GET /api/tyler/parcel/43-510-01-20-00-0-00-000
    [HttpGet("parcel/{pid}")]
    public async Task<ActionResult<ParcelInfoResponse>> GetParcel(string pid, CancellationToken ct)
    {
        var result = await _svc.GetParcelAsync(pid, ct);
        return Ok(result);
    }

    // GET /api/tyler/parcel/{pid}/photos
    [HttpGet("parcel/{pid}/photos")]
    public async Task<ActionResult<object>> GetPhotos(string pid, CancellationToken ct)
    {
        var urls = await _tcm.GetPhotoUrlsAsync(pid, ct);
        return Ok(new { urls });
    }

    // GET /api/tyler/parcel/{pid}/full
    [HttpGet("parcel/{pid}/full")]
    public async Task<ActionResult<object>> GetFull(string pid, CancellationToken ct)
    {
        var infoTask = _svc.GetParcelAsync(pid, ct);
        var photosTask = _tcm.GetPhotoUrlsAsync(pid, ct);

        await Task.WhenAll(infoTask, photosTask);

        return Ok(new
        {
            info = infoTask.Result,
            photos = new { urls = photosTask.Result }
        });
    }
}