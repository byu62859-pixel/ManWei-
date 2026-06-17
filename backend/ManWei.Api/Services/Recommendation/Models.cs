namespace ManWei.Api.Services.Recommendation;

/// <summary>推荐请求（来自 AI tool args 或 REST query string）</summary>
public class RecommendRequest
{
    public string? Keyword { get; set; }       // 可选关键词（用于 Bangumi 搜索补充候选池）
    public string? AnimeType { get; set; }     // 可选类型筛选（TV/剧场版/OVA/WEB），null=不限
    public int TopK { get; set; } = 5;         // 1-20，默认 5
    public bool Deterministic { get; set; } = false; // true=固定Top5(论文复现), false=窗口随机采样
}

/// <summary>单个推荐项的评分拆解（论文要展示）</summary>
public class ScoreBreakdown
{
    public double TagOverlap { get; set; }          // 标签向量点积, [0, 1]
    public double EmotionAffinity { get; set; }     // 情绪亲和度, [0, 1]
    public double QualityBoost { get; set; }        // BangumiScore 归一, [0, 1]
    public double BaseScore { get; set; }           // = 0.6·tag + 0.4·emo (full) 或 1.0·tag (tag_only)
    public double FinalScore { get; set; }          // = baseScore + 0.1·qualityBoost, [0, 1.1]
    public string? NearestNeighborName { get; set; } // 候选番的最近邻 H 中番的 Name（解释用）
    public int? NearestNeighborAnimeId { get; set; } // 最近邻的本地 AnimeId（前端可跳转）
}

/// <summary>推荐项 DTO（最终返回给前端 / AI）</summary>
public class RecommendItem
{
    public int? AnimeId { get; set; }            // 本地 AnimeId（Bangumi 来源的为 null）
    public int? BangumiId { get; set; }          // 外部 ID
    public string Name { get; set; } = string.Empty;
    public string? Cover { get; set; }
    public string? AnimeType { get; set; }
    public double? BangumiScore { get; set; }
    public List<string> Tags { get; set; } = new();       // 候选番的 Top-N 标签名（按 Count 排序）
    public double Score { get; set; }                     // 同 FinalScore（方便前端取）
    public ScoreBreakdown Breakdown { get; set; } = new();
    public string Reason { get; set; } = "";              // 模板化解释，由 ReasonBuilder 生成
}

/// <summary>用户标签画像（TF-IDF + max-pool + L2 归一）</summary>
public class UserTagProfile
{
    /// <summary>U_tag_norm(t) 字典，key=tag 小写 trim, value=归一化权重 ∈ [0, 1]</summary>
    public Dictionary<string, double> Weights { get; set; } = new();
    /// <summary>∑U_tag²=1 是否成立（用于调试/论文验证）</summary>
    public bool IsL2Normalized { get; set; }
    /// <summary>高评分番集合 H 的数量（用于冷启动判定与可解释性）</summary>
    public int HighRatedCount { get; set; }
}

/// <summary>用户情绪画像（avg + std，互补区分"爽/虐"）</summary>
public class EmotionProfile
{
    public double AvgGlobal { get; set; } = 3.0;     // E_avg_global, 1-5；无 H 时默认 3.0
    public double StdGlobal { get; set; } = 0.0;     // E_std_global, ≥ 0
    /// <summary>是否真的有情绪数据（H 中至少 1 部番有 ≥1 条 EmotionRecord）</summary>
    public bool HasProfile { get; set; }
    /// <summary>高评分番中含情绪记录的番数</summary>
    public int CoveredHighRatedCount { get; set; }
}

/// <summary>推荐响应（顶层）</summary>
public class RecommendResult
{
    /// <summary>冷启动模式：full / tag_only / popular</summary>
    public string Mode { get; set; } = "full";
    /// <summary>候选池大小（用于论文实验报告）</summary>
    public int CandidatePoolSize { get; set; }
    public List<RecommendItem> Items { get; set; } = new();
    /// <summary>当 candidates=0 时填充，LLM/前端可基于此回复</summary>
    public string? Error { get; set; }
}
