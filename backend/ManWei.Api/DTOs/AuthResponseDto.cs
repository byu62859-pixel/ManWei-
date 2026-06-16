namespace ManWei.Api.DTOs;

/// <summary>
/// 登录响应（包含 JWT Token）
/// </summary>
public class LoginResponseDto
{
    /// <summary>
    /// JWT Token
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Token 过期时间（秒）
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 昵称
    /// </summary>
    public string? NickName { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    public string Role { get; set; } = "User";
}
