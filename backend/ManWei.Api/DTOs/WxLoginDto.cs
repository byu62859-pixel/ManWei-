using System.ComponentModel.DataAnnotations;

namespace ManWei.Api.DTOs;

/// <summary>
/// 微信小程序登录请求
/// </summary>
public class WxLoginDto
{
    /// <summary>
    /// wx.login() 获取的 code
    /// </summary>
    [Required(ErrorMessage = "code 不能为空")]
    public string Code { get; set; } = string.Empty;
}
