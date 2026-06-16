export interface ApiResponse<T = any> {
  code: number;
  message: string;
  data: T;
}

export interface Anime {
  id: number;
  bangumiId?: number;
  name: string;
  cover?: string;
  summary?: string;
  animeType: string;
  createTime: string;
  favoriteCount: number;
  avgRating?: number;
  reviewCount: number;
  totalEpisodes?: number | null;
  airDate?: string | null;
  duration?: string | null;
  producer?: string | null;
  director?: string | null;
  bangumiScore?: number | null;
  bangumiRank?: number | null;
  bangumiRatingCount?: number | null;
  tags?: AnimeTag[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AnimeQuery {
  page?: number;
  pageSize?: number;
  keyword?: string;
  type?: string;
  tagName?: string;
}

export type AnimeStatus = 'wish' | 'watching' | 'watched';

export interface FavoriteDto {
  id: number;
  animeId: number;
  userId: number;
  status: 0 | 1 | 2;
  progress: number;
  rating: number | null;
  animeName: string;
  animeCover: string | null;
  animeType: string;
  emotionTagNames: string[];
  animeTotalEpisodes?: number | null;
  createTime: string;
}

export interface Favorite {
  id: number;
  animeId: number;
  userId: number;
  status: AnimeStatus;
  rating?: number;
  comment?: string;
  createTime: string;
  anime?: Anime;
}

export interface EmotionCurve {
  episode: number;
  emotionLevel: number;
  createTime: string;
}

export interface Review {
  favoriteId: number;
  content: string;
  createTime: string;
  updateTime: string;
}

// 数据中心类型
export interface UserStats {
  totalEpisodes: number;
  avgRating: number | null;
  totalEpisodesDisplay?: string;
  avgRatingDisplay?: string;
  reviewCount: number;
}

export interface UserProfile {
  id: number;
  openId: string;
  nickName: string | null;
  avatar: string | null;
  role: string;
  isEnabled: boolean;
  createTime: string;
}

export interface WordCloudItem {
  name: string;
  count: number;
}

export interface EmotionCurveWithAnime extends EmotionCurve {
  favoriteId: number;
  animeName: string;
  animeType: string;
}

// 情感标签类型
export interface EmotionTagDto {
  id: number;
  name: string;
  isPreset: boolean;
  animeId: number;
  createTime: string;
}

export interface AnimeTag {
  name: string;
  count: number;
}

// PC AI 助手
export interface ChatMessage {
  id: string;                    // 本地生成的 uuid, 仅用于 React key
  role: 'user' | 'assistant' | 'tool';
  content: string;
  // 一轮 assistant 消息可能触发多个 tool_call, 用数组
  toolCalls?: ChatToolCallItem[];
  isStreaming?: boolean;         // assistant 消息正在流式接收
  isError?: boolean;             // 这一轮流式中断
}

export interface ChatToolCallItem {
  id: string;
  name: string;
  argsJson: string;
  resultJson: string;            // 收到 tool_result 后填充
}

export type ChatStreamEvent =
  | { type: 'delta'; content: string }
  | { type: 'tool_call'; toolCallId: string; toolName: string; toolArgsJson: string }
  | { type: 'tool_result'; toolCallId: string; toolResultJson: string }
  | { type: 'done' }
  | { type: 'error'; error: string };

export interface PcChatRequest {
  message: string;
  history: never[];  // 显式空数组, 标注 server 无状态
}
