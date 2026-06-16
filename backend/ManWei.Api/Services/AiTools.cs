namespace ManWei.Api.Services;

public class AiTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
}

public class AiToolCall
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object?> Arguments { get; set; } = new();
}

public static class PredefinedTools
{
    public static readonly List<AiTool> AllTools = new()
    {
        new AiTool
        {
            Name = "get_user_stats",
            Description = "获取用户全局统计数据，返回总用户数、禁用用户数、管理员数、普通用户数",
            Parameters = """{"type":"object","properties":{}}"""
        },
        new AiTool
        {
            Name = "get_user_list",
            Description = "获取用户列表，支持分页、关键词搜索、状态筛选（enabled/disabled/all）",
            Parameters = """
            {
              "type": "object",
              "properties": {
                "page": {"type": "integer", "description": "页码，默认1"},
                "pageSize": {"type": "integer", "description": "每页数量，默认20，最大100"},
                "keyword": {"type": "string", "description": "搜索关键词（昵称）"},
                "status": {"type": "string", "description": "筛选：enabled/disabled/all，默认all"}
              }
            }
            """
        },
        new AiTool
        {
            Name = "get_user_growth",
            Description = "获取用户增长趋势，按日期统计每日新增用户数",
            Parameters = """
            {
              "type": "object",
              "properties": {
                "days": {"type": "integer", "description": "统计天数，默认30"}
              }
            }
            """
        },
        new AiTool
        {
            Name = "get_anime_stats",
            Description = "获取动漫全局统计数据，返回总动漫数、各类型数量",
            Parameters = """{"type":"object","properties":{}}"""
        },
        new AiTool
        {
            Name = "get_anime_list",
            Description = "获取动漫列表，支持分页、关键词搜索、类型筛选",
            Parameters = """
            {
              "type": "object",
              "properties": {
                "page": {"type": "integer", "description": "页码，默认1"},
                "pageSize": {"type": "integer", "description": "每页数量，默认20"},
                "keyword": {"type": "string", "description": "搜索关键词（动漫名称）"},
                "animeType": {"type": "string", "description": "类型筛选：TV/剧场版/OVA/SP/电影"}
              }
            }
            """
        },
        new AiTool
        {
            Name = "get_anime_rank",
            Description = "获取动漫收藏排行榜，返回收藏数 TOP N 的动漫",
            Parameters = """
            {
              "type": "object",
              "properties": {
                "limit": {"type": "integer", "description": "返回数量，默认10"}
              }
            }
            """
        },
        new AiTool
        {
            Name = "get_favorite_stats",
            Description = "获取收藏全局统计数据，返回总收藏数、各状态（想看/在看/看过）数量",
            Parameters = """{"type":"object","properties":{}}"""
        },
        new AiTool
        {
            Name = "get_favorite_stats_by_anime",
            Description = "获取指定动漫的收藏统计数据",
            Parameters = """
            {
              "type": "object",
              "properties": {
                "animeId": {"type": "integer", "description": "动漫ID"}
              }
            }
            """
        },
        new AiTool
        {
            Name = "get_tag_stats",
            Description = "获取标签使用统计，返回使用频率 TOP N 的标签",
            Parameters = """
            {
              "type": "object",
              "properties": {
                "limit": {"type": "integer", "description": "返回数量，默认10"}
              }
            }
            """
        },
        new AiTool
        {
            Name = "get_tag_wordcloud",
            Description = "获取全局情感词云数据，各标签被使用的总次数",
            Parameters = """{"type":"object","properties":{}}"""
        },
        new AiTool
        {
            Name = "get_review_stats",
            Description = "获取观后感全局统计数据，返回总观后感数",
            Parameters = """{"type":"object","properties":{}}"""
        },
        new AiTool
        {
            Name = "get_review_list",
            Description = "获取观后感列表，支持分页、关键词搜索，返回作者昵称、内容摘要、关联动漫名称",
            Parameters = """
            {
              "type": "object",
              "properties": {
                "page": {"type": "integer", "description": "页码，默认1"},
                "pageSize": {"type": "integer", "description": "每页数量，默认10，最大100"},
                "keyword": {"type": "string", "description": "搜索关键词（观后感内容）"}
              }
            }
            """
        },
        new AiTool
        {
            Name = "get_emotion_curve_stats",
            Description = "获取情感曲线全局统计，各等级（1-5）的记录数分布",
            Parameters = """{"type":"object","properties":{}}"""
        }
    };
}
