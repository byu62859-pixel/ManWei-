namespace ManWei.Api.Models;

public class Review
{
    public int Id { get; set; }
    public int FavoriteId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;

    public Favorite Favorite { get; set; } = null!;
}
