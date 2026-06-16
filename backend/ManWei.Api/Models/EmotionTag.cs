namespace ManWei.Api.Models;

public class EmotionTag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsPreset { get; set; } = false;
    public int? UserId { get; set; }
    public int? AnimeId { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Anime? Anime { get; set; }
}
