using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using TylerInfoAPI.Options;

namespace TylerInfoAPI.Services;

public sealed class TcmSoapService : ITcmService
{
    private readonly IHttpClientFactory _http;
    private readonly TcmOptions _opt;
    private readonly ILogger<TcmSoapService> _log;

    public TcmSoapService(IHttpClientFactory http, IOptions<TcmOptions> opt, ILogger<TcmSoapService> log)
    {
        _http = http;
        _opt = opt.Value;
        _log = log;
    }

    public async Task<IReadOnlyList<string>> GetPhotoUrlsAsync(string parcelNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(parcelNumber))
            return Array.Empty<string>();

        // Tyler/TCM requires NO dashes
        var pid = parcelNumber.Replace("-", "").Trim();

        var docsJson = await GetDocumentsAsJsonAsync(pid, ct);
        if (string.IsNullOrWhiteSpace(docsJson))
            return Array.Empty<string>();

        var docIds = ExtractDocumentIds(docsJson);
        if (docIds.Count == 0)
            return Array.Empty<string>();

        var urls = new List<string>();

        foreach (var docId in docIds)
        {
            ct.ThrowIfCancellationRequested();

            var url = await GetAttachmentAsUrlAsync(docId, ct);
            if (string.IsNullOrWhiteSpace(url))
                continue;

            url = NormalizeAttachmentUrl(url);

            // Match your old behavior: photos only
            if (LooksLikePhoto(url))
                urls.Add(url);
        }

        return urls;
    }

    private async Task<string> GetDocumentsAsJsonAsync(string pidNoDashes, CancellationToken ct)
    {
        var soap = $"""
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
          <soap:Body>
            <getDocumentsAsJson>
              <in>
                <query xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="Search.xsd" resultsLimit="1000">
                  <searchFilters>
                    <And>
                      <Filter name="ParcelNumID" operator="equals">{SecurityElementEscape(pidNoDashes)}</Filter>
                    </And>
                  </searchFilters>
                </query>
              </in>
              <in2>{SecurityElementEscape(_opt.Username)}</in2>
              <in3>{SecurityElementEscape(_opt.Password)}</in3>
            </getDocumentsAsJson>
          </soap:Body>
        </soap:Envelope>
        """;

        var client = _http.CreateClient("tcm");

        using var req = new HttpRequestMessage(HttpMethod.Post, _opt.DocumentEndpoint);
        req.Content = new StringContent(soap, Encoding.UTF8, "text/xml");

        // Some SOAP stacks require quotes; some don’t. This works with most:
        req.Headers.TryAddWithoutValidation("SOAPAction", "\"getDocumentsAsJson\"");

        using var res = await client.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var xml = await res.Content.ReadAsStringAsync(ct);
        return ExtractFirstJsonFromSoap(xml);
    }

    private async Task<string> GetAttachmentAsUrlAsync(string documentId, CancellationToken ct)
    {
        var soap = $"""
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
          <soap:Body>
            <getAttachmentAsUrl>
              <in>{SecurityElementEscape(documentId)}</in>
              <in2>{SecurityElementEscape(_opt.Username)}</in2>
              <in3>{SecurityElementEscape(_opt.Password)}</in3>
            </getAttachmentAsUrl>
          </soap:Body>
        </soap:Envelope>
        """;

        var client = _http.CreateClient("tcm");

        using var req = new HttpRequestMessage(HttpMethod.Post, _opt.DocumentEndpoint);
        req.Content = new StringContent(soap, Encoding.UTF8, "text/xml");
        req.Headers.TryAddWithoutValidation("SOAPAction", "\"getAttachmentAsUrl\"");

        using var res = await client.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var xml = await res.Content.ReadAsStringAsync(ct);
        return ExtractFirstUrlFromSoap(xml);
    }

    private static string ExtractFirstJsonFromSoap(string soapXml)
    {
        // Your exception is happening because your old code extracted "".
        // This method finds the first descendant value that *actually looks like JSON*.
        var xdoc = XDocument.Parse(soapXml);

        foreach (var node in xdoc.Descendants())
        {
            var raw = node.Value;
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var val = WebUtility.HtmlDecode(raw).Trim();
            var t = val.TrimStart();

            if (t.StartsWith("{") || t.StartsWith("["))
                return val;
        }

        return "";
    }

    private static string ExtractFirstUrlFromSoap(string soapXml)
    {
        var xdoc = XDocument.Parse(soapXml);

        foreach (var node in xdoc.Descendants())
        {
            var raw = node.Value;
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var val = WebUtility.HtmlDecode(raw).Trim();
            if (val.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return val;
        }

        return "";
    }

    private static List<string> ExtractDocumentIds(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;

        // Some services wrap results like { "d": [...] }
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("d", out var d) &&
            d.ValueKind == JsonValueKind.Array)
        {
            root = d;
        }

        if (root.ValueKind != JsonValueKind.Array)
            return new List<string>();

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in root.EnumerateArray())
        {
            if (!item.TryGetProperty("attachments", out var atts) || atts.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var att in atts.EnumerateArray())
            {
                if (!att.TryGetProperty("documentId", out var idEl))
                    continue;

                var id = idEl.ValueKind switch
                {
                    JsonValueKind.String => idEl.GetString(),
                    JsonValueKind.Number => idEl.GetInt64().ToString(),
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(id))
                    ids.Add(id);
            }
        }

        return ids.ToList();
    }

    private string NormalizeAttachmentUrl(string url)
    {
        var cleaned = url.Trim();

        // Keep your legacy replacement behavior (internal -> public)
        if (!string.IsNullOrWhiteSpace(_opt.InternalUrlPrefix) && !string.IsNullOrWhiteSpace(_opt.PublicUrlPrefix))
        {
            cleaned = cleaned.Replace(_opt.InternalUrlPrefix, _opt.PublicUrlPrefix, StringComparison.OrdinalIgnoreCase);
        }

        return cleaned;
    }

    private static bool LooksLikePhoto(string url)
    {
        var u = url.ToLowerInvariant();

        // matches your old UI logic
        if (!u.Contains("doccconv"))
            return false;

        return u.EndsWith(".jpg") || u.EndsWith(".jpeg") || u.EndsWith(".png") || u.EndsWith(".webp");
    }

    private static string SecurityElementEscape(string s) => System.Security.SecurityElement.Escape(s) ?? "";
}