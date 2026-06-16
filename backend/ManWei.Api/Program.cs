using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ManWei.Api.Data;
using ManWei.Api.Common;
using ManWei.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 注册 AppDbContext，使用 appsettings.json 中的连接字符串
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 注册 HttpClient for Bangumi API（带 User-Agent 和 Bearer Token）
builder.Services.AddHttpClient<IBangumiService, BangumiService>((services, client) =>
{
    client.BaseAddress = new Uri(builder.Configuration["BangumiApi:BaseUrl"] ?? "https://api.bgm.tv/v0");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        builder.Configuration["BangumiApi:UserAgent"] ?? "ManWei-App/1.0 (Student-Project)");
    client.Timeout = TimeSpan.FromSeconds(30);

    // 添加 Bearer Token 认证
    var accessToken = builder.Configuration["BangumiApi:AccessToken"];
    if (!string.IsNullOrWhiteSpace(accessToken))
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    }
});

// 注册 DeepSeek HttpClient
builder.Services.AddHttpClient("DeepSeek", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com");
    client.Timeout = TimeSpan.FromSeconds(120);
});

// 注册 AI Agent Service
builder.Services.AddScoped<IAiAgentService, AiAgentService>();
builder.Services.AddScoped<WxAiAgentService>();

// 注册 Bangumi 全局限流器（Singleton，跨 Scoped 服务共享）
builder.Services.AddSingleton<BangumiRateLimiter>();

// 注册 Bangumi 同步后台服务（每小时检查一次）
builder.Services.AddHostedService<BangumiSyncBackgroundService>();

// 配置 JWT 身份认证
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key 未配置");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ManWei.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ManWei.Client";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

// 配置 CORS（允许 PC 管理端跨域访问）
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configure Swagger 支持 Bearer Token
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "漫味 API",
        Version = "v1",
        Description = "动漫情感管理平台 - API 文档"
    });

    // 添加 Bearer Token 认证
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "漫味 API v1");
});

app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 启动时初始化预置数据
await DataSeeder.SeedAsync(app.Services);

// 手动迁移：由于 EF snapshot 有 Review 双重配置的预置 bug，
// 无法通过 dotnet ef database update 迁移，在启动时手动补列
await ManualMigration.RunAsync(app.Services);

app.Run();
