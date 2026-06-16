namespace ManWei.Api.Models;

public class SystemConfig
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTime? LastUpdated { get; set; }
}
