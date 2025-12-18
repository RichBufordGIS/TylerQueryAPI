namespace TylerInfoAPI.Services;

public interface ITokenService
{
    Task<string> GetTokenAsync(CancellationToken ct);
}
