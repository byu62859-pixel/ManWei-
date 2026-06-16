namespace ManWei.Api.Services;

public static class PcAiTools
{
    public static readonly List<AiTool> AllTools = new()
    {
        new AiTool
        {
            Name = "query_my_favorites",
            Description = "查询当前用户的收藏列表。" +
                          "可选参数: status (0=想看 1=在看 2=看过, 不传=全部), " +
                          "take (返回数量, 默认10, 最大50)。" +
                          "返回字段: id, animeId, animeName, status, progress, rating。",
            Parameters = """
                {
                    "type": "object",
                    "properties": {
                        "status": { "type": "integer", "enum": [0, 1, 2] },
                        "take": { "type": "integer", "minimum": 1, "maximum": 50 }
                    }
                }
                """
        },
        new AiTool
        {
            Name = "query_user_stats",
            Description = "查询当前用户的追番统计: " +
                          "收藏总数、在看数量、已看数量、平均评分(1-10, 可能 null)。" +
                          "无入参。",
            Parameters = """{"type":"object","properties":{}}"""
        },
        new AiTool
        {
            Name = "query_anime_emotion_curve",
            Description = "查询用户对某部动漫的情绪曲线。" +
                          "必须参数: animeId (整数)。" +
                          "若用户未收藏该动漫则返回 error=not_favorited。" +
                          "返回: animeId, favoriteId, pointCount, " +
                          "points: [{episode, emotionLevel}]。",
            Parameters = """
                {
                    "type": "object",
                    "properties": {
                        "animeId": { "type": "integer" }
                    },
                    "required": ["animeId"]
                }
                """
        },
        new AiTool
        {
            Name = "search_anime",
            Description = "按关键词搜索动漫(本版未实现, 调用将返回 not_implemented)。",
            Parameters = """
                {
                    "type": "object",
                    "properties": { "keyword": { "type": "string" } }
                }
                """
        },
        new AiTool
        {
            Name = "query_global_emotion_tags",
            Description = "查询用户常用的情绪标签(本版未实现, 调用将返回 not_implemented)。",
            Parameters = """{"type":"object","properties":{}}"""
        }
    };
}
