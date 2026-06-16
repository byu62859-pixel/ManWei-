namespace ManWei.Api.Models;

public class Favorite
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int AnimeId { get; set; }
    public int Status { get; set; } = 0; // 0:想看 1:在看 2:看过
    public int Progress { get; set; } = 0;
    /// <summary>
    /// 用户评分 1-10，null=未评分
    /// </summary>
    public int? Rating { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Anime Anime { get; set; } = null!;
    public Review? Review { get; set; }
    public ICollection<EmotionRecord> EmotionRecords { get; set; } = new List<EmotionRecord>();
}
