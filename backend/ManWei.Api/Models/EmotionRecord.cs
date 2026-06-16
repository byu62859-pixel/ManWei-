namespace ManWei.Api.Models;

public class EmotionRecord
{
    public int Id { get; set; }
    public int FavoriteId { get; set; }
    public int EmotionLevel { get; set; } // 1-5 档情绪值
    public int Episode { get; set; } = 1;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public Favorite Favorite { get; set; } = null!;
}
