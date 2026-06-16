export type FavoriteStatus = 0 | 1 | 2;

export interface FavoriteDto {
  id: number;
  animeId: number;
  userId: number;
  status: FavoriteStatus;
  progress: number;
  rating: number | null;
  animeName: string;
  animeCover: string | null;
  animeType: string;
  emotionTagNames: string[];
  animeTotalEpisodes?: number | null;
  createTime: string;
}

export interface FavoriteQuery {
  page: number;
  pageSize: number;
  status?: FavoriteStatus | null;
  tagName?: string;
  orderBy?: string;
}

export interface FavoriteUpdateDto {
  status?: FavoriteStatus;
  progress?: number;
  rating?: number | null;
}

export const STATUS_LABELS: Record<FavoriteStatus, string> = {
  0: '想看',
  1: '在看',
  2: '看过',
};

// 搜索结果条目（对应后端 AnimeSearchResultDto）
export interface AnimeSearchResultItem {
  animeId: number | null;    // 本地库有则有值
  bangumiId: number;         // 始终有值
  name: string;             // NameCn ?? Name
  nameCn: string | null;
  cover: string | null;
  animeType: string;
  source: 'local' | 'bangumi';
}

// 添加收藏请求参数（对应后端 AddFavoriteByBangumiDto）
export interface AddFavoriteParams {
  animeId?: number;
  bangumiId?: number;
}