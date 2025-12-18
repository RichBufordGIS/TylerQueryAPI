namespace TylerInfoAPI.Models;

public sealed class Root
{
    public string PacketName { get; set; } = "";
    public List<PacketRow> PacketRows { get; set; } = new();
}

public sealed class PacketRow
{
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Zip { get; set; } = "";
    public string Acres { get; set; } = "";
    public string Land_Use_Code_Descr { get; set; } = "";

    public string Living_Area { get; set; } = "";
    public string Bedrooms { get; set; } = "";

    public string Tax_District { get; set; } = "";
    public string Legal_Descr { get; set; } = "";

    public string Year { get; set; } = "";
    public string Exemption_Code { get; set; } = "";

    public string Tax_Year { get; set; } = "";
    public string Total_Market { get; set; } = "";
    public string Total_Assessed { get; set; } = "";
    public string Total_Taxable { get; set; } = "";

    public string Owner { get; set; } = "";
    public string Owner_Address { get; set; } = "";
}

