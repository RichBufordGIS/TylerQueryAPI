namespace TylerInfoAPI.Options;

public sealed class TylerOptions
{
    public string TokenFolder { get; set; } = @"C:\TylerToken";
    public int TaxYear { get; set; } = 2025;
    public string District { get; set; } = "048";
    public string DataletName { get; set; } = "rp_parcel_viewer";
    public string BaseUrl { get; set; } = "";
}

