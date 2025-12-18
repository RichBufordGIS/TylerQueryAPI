using TylerInfoAPI.Models;

namespace TylerInfoAPI.Services;

public interface ITylerService
{
    Task<ParcelInfoResponse> GetParcelAsync(string pid, CancellationToken ct);
}

