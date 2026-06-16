using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManWei.Api.Common;
using ManWei.Api.Data;
using ManWei.Api.DTOs;
using ManWei.Api.Models;

namespace ManWei.Api.Controllers;

/// <summary>
/// 用户管理控制器（PC端）
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private static readonly Dictionary<string, string> AvatarExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };
    private const long MaxAvatarBytes = 2 * 1024 * 1024;

    public UsersController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    /// <summary>
    /// 获取用户列表（分页）
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>用户分页列表</returns>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Result<PagedResult<UserDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Result<PagedResult<UserDto>>>> GetList([FromQuery] UserQueryDto query)
    {
        var queryable = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            queryable = queryable.Where(u =>
                u.OpenId.Contains(query.Keyword) ||
                (u.NickName != null && u.NickName.Contains(query.Keyword)));
        }

        var totalCount = await queryable.CountAsync();

        var items = await queryable
            .OrderByDescending(u => u.CreateTime)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new UserDto
            {
                Id = u.Id,
                OpenId = u.OpenId,
                NickName = u.NickName,
                Avatar = u.Avatar,
                Role = u.Role,
                IsEnabled = u.IsEnabled,
                CreateTime = u.CreateTime
            })
            .ToListAsync();

        var result = new PagedResult<UserDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };

        return Ok(Result<PagedResult<UserDto>>.Success(result));
    }

    /// <summary>
    /// 获取用户详情
    /// </summary>
    /// <param name="id">用户ID</param>
    /// <returns>用户详情</returns>
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Result<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<UserDto>>> GetById(int id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
            return NotFound(Result.Fail(404, "用户不存在"));

        var dto = new UserDto
        {
            Id = user.Id,
            OpenId = user.OpenId,
            NickName = user.NickName,
            Avatar = user.Avatar,
            Role = user.Role,
            IsEnabled = user.IsEnabled,
            CreateTime = user.CreateTime
        };

        return Ok(Result<UserDto>.Success(dto));
    }

    /// <summary>
    /// 更新用户状态（启用/禁用）
    /// </summary>
    /// <param name="id">用户ID</param>
    /// <param name="dto">状态更新</param>
    /// <returns>更新结果</returns>
    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Result<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<UserDto>>> UpdateStatus(int id, [FromBody] UpdateUserStatusDto dto)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
            return NotFound(Result.Fail(404, "用户不存在"));

        // 禁止禁用自己
        var currentUserId = GetCurrentUserId();
        if (currentUserId != null && user.Id == currentUserId.Value)
            return BadRequest(Result.Fail(400, "不能禁用自己的账号"));

        user.IsEnabled = dto.IsEnabled;
        await _context.SaveChangesAsync();

        var resultDto = new UserDto
        {
            Id = user.Id,
            OpenId = user.OpenId,
            NickName = user.NickName,
            Avatar = user.Avatar,
            Role = user.Role,
            IsEnabled = user.IsEnabled,
            CreateTime = user.CreateTime
        };

        return Ok(Result<UserDto>.Success(resultDto, dto.IsEnabled ? "已启用" : "已禁用"));
    }

    /// <summary>
    /// 删除用户
    /// </summary>
    /// <param name="id">用户ID</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Result), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result>> Delete(int id)
    {
        // 禁止删除自己
        var currentUserId = GetCurrentUserId();
        if (currentUserId != null && id == currentUserId.Value)
            return BadRequest(Result.Fail(400, "不能删除自己的账号"));

        var user = await _context.Users.FindAsync(id);

        if (user == null)
            return NotFound(Result.Fail(404, "用户不存在"));

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(Result.Success("删除成功"));
    }

    /// <summary>
    /// 获取当前用户资料
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(Result<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<UserDto>>> GetMe()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null)
            return NotFound(Result.Fail(404, "用户不存在"));

        return Ok(Result<UserDto>.Success(ToUserDto(user)));
    }

    /// <summary>
    /// 修改当前用户昵称
    /// </summary>
    [HttpPut("me/nickname")]
    [Authorize]
    [ProducesResponseType(typeof(Result<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Result<UserDto>>> UpdateNickname([FromBody] UpdateNicknameRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.NickName))
            return BadRequest(Result.Fail(400, "昵称不能为空"));

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null)
            return NotFound(Result.Fail(404, "用户不存在"));

        user.NickName = request.NickName.Trim();
        await _context.SaveChangesAsync();

        var dto = new UserDto
        {
            Id = user.Id,
            OpenId = user.OpenId,
            NickName = user.NickName,
            Avatar = user.Avatar,
            Role = user.Role,
            IsEnabled = user.IsEnabled,
            CreateTime = user.CreateTime
        };

        return Ok(Result<UserDto>.Success(dto, "昵称修改成功"));
    }

    /// <summary>
    /// 上传当前用户头像
    /// </summary>
    [HttpPost("me/avatar")]
    [Authorize]
    [RequestSizeLimit(3 * 1024 * 1024)]
    [ProducesResponseType(typeof(Result<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Result<UserDto>>> UploadAvatar([FromForm] IFormFile file)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(Result.Fail(400, "请选择头像文件"));

        if (file.Length > MaxAvatarBytes)
            return BadRequest(Result.Fail(400, "头像不能超过2MB"));

        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !AvatarExtensions.TryGetValue(file.ContentType, out var extension))
        {
            return BadRequest(Result.Fail(400, "仅支持 JPG、PNG、WEBP 图片"));
        }

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null)
            return NotFound(Result.Fail(404, "用户不存在"));

        var uploadRoot = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads", "avatars");
        Directory.CreateDirectory(uploadRoot);

        var fileName = $"{userId.Value}_{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadRoot, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        user.Avatar = $"/uploads/avatars/{fileName}";
        await _context.SaveChangesAsync();

        return Ok(Result<UserDto>.Success(ToUserDto(user), "头像上传成功"));
    }

    /// <summary>
    /// 获取当前用户追番统计数据
    /// </summary>
    [HttpGet("me/stats")]
    [Authorize]
    [ProducesResponseType(typeof(Result<UserStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Result<UserStatsDto>>> GetMyStats()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var totalEpisodes = await _context.EmotionRecords
            .CountAsync(r => _context.Favorites
                .Where(f => f.UserId == userId.Value)
                .Select(f => f.Id)
                .Contains(r.FavoriteId));

        var avgRating = await _context.Favorites
            .Where(f => f.UserId == userId.Value && f.Rating != null)
            .AverageAsync(f => (double?)f.Rating);

        var reviewCount = await _context.Reviews
            .CountAsync(r => _context.Favorites
                .Where(f => f.UserId == userId.Value)
                .Select(f => f.Id)
                .Contains(r.FavoriteId));

        var stats = new UserStatsDto
        {
            TotalEpisodes = totalEpisodes,
            AvgRating = avgRating,
            ReviewCount = reviewCount
        };

        return Ok(Result<UserStatsDto>.Success(stats));
    }

    /// <summary>
    /// 从 Claims 获取当前用户ID
    /// </summary>
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return null;
        return userId;
    }

    private static UserDto ToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            OpenId = user.OpenId,
            NickName = user.NickName,
            Avatar = user.Avatar,
            Role = user.Role,
            IsEnabled = user.IsEnabled,
            CreateTime = user.CreateTime
        };
    }
}
