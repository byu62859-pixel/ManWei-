export interface ReviewDto {
  reviewId: number;
  favoriteId: number;
  animeId: number;
  animeName: string;
  animeCover: string | null;
  content: string;
  contentSummary: string;
  createTime: string;
  updatedAt: string;
}

export interface ReviewQuery {
  page: number;
  pageSize: number;
}

export interface ReviewUpdateDto {
  favoriteId: number;
  content: string;
}