using System.ComponentModel.DataAnnotations;

namespace ManWei.Api.DTOs;

/// <summary>
/// 用户注册请求
/// </summary>
public class RegisterDto
{
    /// <summary>
    /// 用户名（登录账号）
    /// </summary>
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "用户名长度在 3-50 个字符之间")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码
    /// </summary>
    [Required(ErrorMessage = "密码不能为空")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度在 6-100 个字符之间")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 昵称（可选）
    /// </summary>
    [StringLength(50, ErrorMessage = "昵称长度不能超过 50 个字符")]
    public string? NickName { get; set; }
}
