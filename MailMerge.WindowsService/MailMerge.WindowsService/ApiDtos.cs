using System.Text.Json.Serialization;

// Top-level object structure from the API
public class ApiResponse
{
    [JsonPropertyName("results")]
    public List<PropertyResultDto> Results { get; set; }

    [JsonPropertyName("totalCost")]
    public decimal TotalCost { get; set; }

    [JsonPropertyName("resultCount")]
    public int ResultCount { get; set; }

    [JsonPropertyName("totalResultCount")]
    public int TotalResultCount { get; set; }
}

// Represents a single property record within the "results" array
public class PropertyResultDto
{
    [JsonPropertyName("State")]
    public string State { get; set; }
    [JsonPropertyName("City")]
    public string City { get; set; }
    [JsonPropertyName("ZipFive")]
    public string ZipFive { get; set; }
    [JsonPropertyName("Address")]
    public string Address { get; set; }
    [JsonPropertyName("RadarID")]
    public string RadarID { get; set; }
    [JsonPropertyName("APN")]
    public string APN { get; set; }
    [JsonPropertyName("PType")]
    public string PType { get; set; }
    [JsonPropertyName("OwnershipType")]
    public string OwnershipType { get; set; }
    [JsonPropertyName("Owner")]
    public string Owner { get; set; }
    [JsonPropertyName("OwnerAddress")]
    public string OwnerAddress { get; set; }
    [JsonPropertyName("OwnerCity")]
    public string OwnerCity { get; set; }
    [JsonPropertyName("OwnerZipFive")]
    public string OwnerZipFive { get; set; }
    [JsonPropertyName("OwnerState")]
    public string OwnerState { get; set; }
    [JsonPropertyName("inForeclosure")]
    public int? InForeclosure { get; set; }
    [JsonPropertyName("isSameMailing")]
    public int? IsSameMailing { get; set; }
    [JsonPropertyName("PrimaryName")]
    public string PrimaryName { get; set; }
    [JsonPropertyName("PrimaryFirstName")]
    public string PrimaryFirstName { get; set; }

    // NEW FIELDS
    [JsonPropertyName("ForeclosureStage")]
    public string ForeclosureStage { get; set; }

    [JsonPropertyName("ForeclosureDocType")]
    public string ForeclosureDocType { get; set; }

    [JsonPropertyName("ForeclosureRecDate")]
    public string ForeclosureRecDate { get; set; } // Assuming date is returned as string

    [JsonPropertyName("Trustee")]
    public string Trustee { get; set; }

    [JsonPropertyName("TrusteePhone")]
    public string TrusteePhone { get; set; }

    [JsonPropertyName("TrusteeSaleNum")]
    public string TrusteeSaleNum { get; set; }
}