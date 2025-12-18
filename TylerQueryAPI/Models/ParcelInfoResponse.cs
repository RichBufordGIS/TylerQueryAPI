namespace TylerInfoAPI.Models;

public sealed class ParcelInfoResponse
{
    public string ParcelId { get; set; } = "";
    public string SitusAddress { get; set; } = "";
    public string SitusCityStateZip { get; set; } = "";
    public string LotSize { get; set; } = "";
    public string LandUse { get; set; } = "";
    public string TaxDistrict { get; set; } = "";
    public string Exemption { get; set; } = "";
    public string LegalDescription { get; set; } = "";

    public List<BuildingCard> Buildings { get; set; } = new();
    public List<YearValue> Values { get; set; } = new();
    public List<OwnerInfo> Owners { get; set; } = new();
}

public sealed class BuildingCard
{
    public int Number { get; set; }
    public string LivingArea { get; set; } = "";
    public string Bedrooms { get; set; } = "";
}

public sealed class YearValue
{
    public string Year { get; set; } = "";
    public string TotalMarket { get; set; } = "";
    public string TotalAssessed { get; set; } = "";
    public string TotalTaxable { get; set; } = "";
}

public sealed class OwnerInfo
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
}

