using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ManWei.Api.Common;
using ManWei.Api.Data;
using ManWei.Api.DTOs;
using ManWei.Api.Models;

namespace ManWei.Api.Controllers;

/// <summary>
/// 用户认证控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    /// <param name="dto">注册信息</param>
    /// <returns>用户信息</returns>
    [HttpPost("Register")]
    [ProducesResponseType(typeof(Result<User>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<User>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<User>>> Register([FromBody] RegisterDto dto)
    {
        // 检查用户名是否已存在
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.OpenId == dto.Username);

        if (existingUser != null)
        {
            return BadRequest(Result<User>.Fail(400, "用户名已存在"));
        }

        // 创建新用户
        var user = new User
        {
            OpenId = dto.Username,
            NickName = dto.NickName ?? dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            IsEnabled = true,
            CreateTime = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(Result<User>.Success(user, "注册成功"));
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="dto">登录信息</param>
    /// <returns>JWT Token</returns>
    [HttpPost("Login")]
    [ProducesResponseType(typeof(Result<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<LoginResponseDto>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Result<LoginResponseDto>>> Login([FromBody] LoginDto dto)
    {
        // 查找用户
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.OpenId == dto.Username);

        if (user == null)
        {
            return Unauthorized(Result<LoginResponseDto>.Fail(401, "用户名或密码错误"));
        }

        // 验证密码
        if (string.IsNullOrEmpty(user.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return Unauthorized(Result<LoginResponseDto>.Fail(401, "用户名或密码错误"));
        }

        // 检查用户是否被禁用
        if (!user.IsEnabled)
        {
            return Unauthorized(Result<LoginResponseDto>.Fail(401, "账号已被禁用"));
        }

        // 生成 JWT Token
        var token = GenerateJwtToken(user);
        var expiresIn = int.Parse(_configuration["Jwt:ExpiresInSeconds"] ?? "604800");

        var response = new LoginResponseDto
        {
            Token = token,
            ExpiresIn = expiresIn,
            UserId = user.Id,
            NickName = user.NickName,
            Role = user.Role
        };

        return Ok(Result<LoginResponseDto>.Success(response, "登录成功"));
    }

    /// <summary>
    /// 微信小程序登录
    /// </summary>
    /// <param name="dto">wx.login() 获取的 code</param>
    /// <returns>JWT Token</returns>
    [HttpPost("wx-login")]
    [ProducesResponseType(typeof(Result<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<LoginResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Result<LoginResponseDto>>> WxLogin([FromBody] WxLoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            return BadRequest(Result<LoginResponseDto>.Fail(400, "code 不能为空"));
        }

        // 调用微信接口换取 openid
        var appId = _configuration["WxMiniApp:AppId"];
        var appSecret = _configuration["WxMiniApp:AppSecret"];
        var wxUrl = $"https://api.weixin.qq.com/sns/jscode2session?appid={appId}&secret={appSecret}&js_code={dto.Code}&grant_type=authorization_code";

        string openid;
        try
        {
            using var httpClient = new HttpClient();
            var wxResponse = await httpClient.GetStringAsync(wxUrl);
            using var doc = JsonDocument.Parse(wxResponse);

            if (doc.RootElement.TryGetProperty("errcode", out var errcode) && errcode.GetInt32() != 0)
            {
                var errmsg = doc.RootElement.TryGetProperty("errmsg", out var em) ? em.GetString() : "微信登录失败";
                return BadRequest(Result<LoginResponseDto>.Fail(400, errmsg!));
            }

            openid = doc.RootElement.GetProperty("openid").GetString()!;
        }
        catch (Exception ex)
        {
            return BadRequest(Result<LoginResponseDto>.Fail(400, $"微信服务调用失败: {ex.Message}"));
        }

        // 查找或创建用户
        var user = await _context.Users.FirstOrDefaultAsync(u => u.OpenId == openid);
        if (user == null)
        {
            user = new User
            {
                OpenId = openid,
                NickName = $"用户_{openid[..8]}",
                IsEnabled = true,
                CreateTime = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        else if (!user.IsEnabled)
        {
            return Unauthorized(Result<LoginResponseDto>.Fail(401, "账号已被禁用"));
        }

        // 生成 JWT Token
        var token = GenerateJwtToken(user);
        var expiresIn = int.Parse(_configuration["Jwt:ExpiresInSeconds"] ?? "604800");

        var response = new LoginResponseDto
        {
            Token = token,
            ExpiresIn = expiresIn,
            UserId = user.Id,
            NickName = user.NickName,
            Role = user.Role
        };

        return Ok(Result<LoginResponseDto>.Success(response, "登录成功"));
    }

    /// <summary>
    /// 生成 JWT Token
    /// </summary>
    private string GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.NickName ?? user.OpenId),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("username", user.OpenId)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(int.Parse(_configuration["Jwt:ExpiresInSeconds"] ?? "604800")),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
