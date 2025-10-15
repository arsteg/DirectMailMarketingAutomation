using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class PropertyRecord
{
    [Key]
    public int Id { get; set; } // DB identity key

    [MaxLength(64)]
    public string RadarId { get; set; }

    [MaxLength(64)]
    public string Apn { get; set; }

    [MaxLength(64)]
    public string Type { get; set; }

    [MaxLength(256)]
    public string Address { get; set; }

    [MaxLength(128)]
    public string City { get; set; }

    [MaxLength(32)]
    public string State { get; set; }

    // Keep ZIP as string to preserve leading zeros
    [MaxLength(16)]
    public string Zip { get; set; }

    [MaxLength(256)]
    public string Owner { get; set; }

    [MaxLength(128)]
    public string OwnerType { get; set; }

    [MaxLength(32)]
    public string OwnerOcc { get; set; } // raw text ("Yes/No" etc.)

    [MaxLength(256)]
    public string PrimaryName { get; set; }

    [MaxLength(128)]
    public string PrimaryFirst { get; set; }

    [MaxLength(256)]
    public string MailAddress { get; set; }

    [MaxLength(128)]
    public string MailCity { get; set; }

    [MaxLength(32)]
    public string MailState { get; set; }

    [MaxLength(16)]
    public string MailZip { get; set; }

    [MaxLength(32)]
    public string Foreclosure { get; set; }

    [MaxLength(64)]
    public string FclStage { get; set; }

    [MaxLength(64)]
    public string FclDocType { get; set; }

    public string FclRecDate { get; set; }

    [MaxLength(256)]
    public string Trustee { get; set; }

    [MaxLength(128)]
    public string TsNumber { get; set; }

    [MaxLength(64)]
    public string TrusteePhone { get; set; }
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
}