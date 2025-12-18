using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TylerInfoAPI.Options;

namespace TylerInfoAPI.Services;

public sealed class TokenService : ITokenService
{
    private readonly IMemoryCache _cache;
    private readonly TylerOptions _opt;

    public TokenService(IMemoryCache cache, IOptions<TylerOptions> opt)
    {
        _cache = cache;
        _opt = opt.Value;
    }

    public Task<string> GetTokenAsync(CancellationToken ct)
    {
        var hour = DateTime.Now.Hour;
        var cacheKey = $"tyler-token-{hour}";

        if (_cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrWhiteSpace(cached))
            return Task.FromResult(cached);

        string ReadTokenFile(int h)
        {
            var path = Path.Combine(_opt.TokenFolder, $"TCMtoken{h}.txt");
            using var tr = new StreamReader(path);

            // skip first 3 lines
            for (int i = 0; i < 3; i++) tr.ReadLine();

            return (tr.ReadToEnd() ?? "").Trim();
        }

        string token = "";
        try { token = ReadTokenFile(hour); } catch { /* fallback */ }

        if (string.IsNullOrWhiteSpace(token))
        {
            var prev = hour == 0 ? 23 : hour - 1;
            token = ReadTokenFile(prev);
        }

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Tyler token file was blank or unreadable.");

        _cache.Set(cacheKey, token, TimeSpan.FromMinutes(55));
        return Task.FromResult(token);
    }
}

