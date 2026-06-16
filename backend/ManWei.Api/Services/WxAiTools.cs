namespace ManWei.Api.Services;

public class WxAiTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
}

public static class WxPredefinedTools
{
    public static readonly List<WxAiTool> AllTools = new()
    {
        new WxAiTool
        {
            Name = "get_my_favorites",
            Description = "查询当前用户的收藏列表，支持按状态和关键词筛选",
            Parameters = """
            {
              "type": "object",
              "properties": {
                "page": {"type": "integer", "description": "页码，默认1"},
                "pageSize": {"type": "integer", "description": "每页数量，默认20，最大100"},
                "status": {"type": "integer", "description": "收藏状态：0=想看、1=在看、2=看过，不传则返回全部"},
                "keyword": {"type": "string", "description": "搜索关键词（动漫名称）"}
              }
            }
            """
        },
        new WxAiTool
        {
            Name = "get_my_reviews",
            Description = "查询当前用户的观后感列表",
            Parameters = """
            {
              "type": "object",
              "properties": {
                "page": {"type": "integer", "description": "页码，默认1"},
                "pageSize": {"type": "integer", "description": "每页数量，默认10"},
                "keyword": {"type": "string", "description": "搜索关键词（内容）"}
              }
            }
            """
        },
        new WxAiTool
        {
            Name = "get_my_stats",
            Description = "查询当前用户追番统计数据，返回总收藏数、各状态数量、总集数、平均评分、观后感数",
            Parameters = """{"type":"object","properties":{}}"""
        },
        new WxAiTool
        {
            Name = "get_my_emotion_curves",
            Description = "查询当前用户的情感曲线数据，只返回最近5部在追动漫的记录，避免数据量过大",
            Parameters = """
            {
              "type": "object",
              "properties": {
                "animeId": {"type": "integer", "description": "指定动漫ID，不传则返回最近5部"}
              }
            }
            """
        },
        new WxAiTool
        {
            Name = "get_anime_detail",
            Description = "查询指定动漫的详细信息",
            Parameters = """
            {
              "type": "object",
              "properties": {
                "animeId": {"type": "integer", "description": "动漫ID"}
              }
            }
            """
        },
        new WxAiTool
        {
            Name = "search_anime",
            Description = "在全局动漫库搜索动漫，支持关键词搜索、类型筛选、分页",
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
        }
    };
}
