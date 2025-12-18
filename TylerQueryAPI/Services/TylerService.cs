using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TylerInfoAPI.Models;
using TylerInfoAPI.Options;

namespace TylerInfoAPI.Services;

public sealed class TylerService : ITylerService
{
    private readonly IHttpClientFactory _http;
    private readonly ITokenService _tokens;
    private readonly TylerOptions _opt;

    public TylerService(IHttpClientFactory http, ITokenService tokens, IOptions<TylerOptions> opt)
    {
        _http = http;
        _tokens = tokens;
        _opt = opt.Value;
    }

    public async Task<ParcelInfoResponse> GetParcelAsync(string pid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pid))
            throw new ArgumentException("PID is required.", nameof(pid));

        var parcelNoDashes = pid.Replace("-", "");
        var token = await _tokens.GetTokenAsync(ct);

        var url = $"{_opt.BaseUrl}/{_opt.TaxYear}/{_opt.District}/{parcelNoDashes}/{_opt.DataletName}?token={Uri.EscapeDataString(token)}";

        var client = _http.CreateClient("tyler");
        var json = await client.GetStringAsync(url, ct);

        var roots = JsonSerializer.Deserialize<Root[]>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? Array.Empty<Root>();

        static PacketRow? Row(Root[] r, int packetIndex, int rowIndex = 0)
            => (r.Length > packetIndex && r[packetIndex]?.PacketRows?.Count > rowIndex)
                ? r[packetIndex].PacketRows[rowIndex]
                : null;

        var r2 = Row(roots, 2);
        var r3 = Row(roots, 3);
        var r4 = Row(roots, 4);
        var r21 = Row(roots, 21);

        var resp = new ParcelInfoResponse
        {
            ParcelId = pid,
            SitusAddress = r2?.Address ?? "",
            SitusCityStateZip = $"{r2?.City}, {r2?.State} {r2?.Zip}".Replace(" ,", "").Trim(),
            LandUse = r2?.Land_Use_Code_Descr ?? "",
            TaxDistrict = r3?.Tax_District ?? "",
            LegalDescription = r4?.Legal_Descr ?? "",
            Exemption = (!string.IsNullOrWhiteSpace(r21?.Year) ? (r21?.Exemption_Code ?? "") : "No exemptions")
        };

        // Lot size formatting
        if (double.TryParse(r2?.Acres, NumberStyles.Any, CultureInfo.InvariantCulture, out var acres))
        {
            resp.LotSize = acres > 1.0
                ? $"{acres} Acres"
                : $"{(acres * 43560):N0} Sq Ft";
        }

        // Buildings (packet 10)
        if (roots.Length > 10)
        {
            var cards = roots[10].PacketRows;
            for (int i = 0; i < cards.Count; i++)
            {
                resp.Buildings.Add(new BuildingCard
                {
                    Number = i + 1,
                    LivingArea = string.IsNullOrWhiteSpace(cards[i].Living_Area) ? "" : $"{cards[i].Living_Area} Sq Ft",
                    Bedrooms = string.IsNullOrWhiteSpace(cards[i].Bedrooms) ? "" : $"{cards[i].Bedrooms} Bedrooms"
                });
            }
        }

        // Values packets 14..19 (6 years)
        for (int idx = 14; idx <= 19; idx++)
        {
            var v = Row(roots, idx);
            if (v == null) continue;

            if (string.IsNullOrWhiteSpace(v.Tax_Year))
            {
                resp.Values.Add(new YearValue
                {
                    Year = $"Parcel data did not exist in {_opt.TaxYear - (idx - 14)}"
                });
                continue;
            }

            resp.Values.Add(new YearValue
            {
                Year = v.Tax_Year,
                TotalMarket = v.Total_Market,
                TotalAssessed = v.Total_Assessed,
                TotalTaxable = v.Total_Taxable
            });
        }

        // Owners packets 7 and 8
        var owners = (roots.Length > 7) ? roots[7].PacketRows : new List<PacketRow>();
        var addrs = (roots.Length > 8) ? roots[8].PacketRows : new List<PacketRow>();

        for (int i = 0; i < owners.Count; i++)
        {
            resp.Owners.Add(new OwnerInfo
            {
                Name = owners[i].Owner ?? "",
                Address = (addrs.Count > i ? addrs[i].Owner_Address : "") ?? ""
            });
        }

        return resp;
    }
}
