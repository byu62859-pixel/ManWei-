namespace ManWei.Api.Models;

public class User
{
    public int Id { get; set; }
    public string OpenId { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public string? Avatar { get; set; }
    public string? PasswordHash { get; set; }
    public string Role { get; set; } = "User";
    public bool IsEnabled { get; set; } = true;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<EmotionTag> EmotionTags { get; set; } = new List<EmotionTag>();
}
