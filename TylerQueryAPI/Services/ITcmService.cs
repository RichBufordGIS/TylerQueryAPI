namespace TylerInfoAPI.Services;

public interface ITcmService
{
    Task<IReadOnlyList<string>> GetPhotoUrlsAsync(string parcelNumber, CancellationToken ct = default);
}