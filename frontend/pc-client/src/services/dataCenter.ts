import request from './request';
import type { UserStats, WordCloudItem, EmotionCurve, FavoriteDto } from '../types/api';

export const getUserStats = async (): Promise<UserStats> => {
  const res = await request.get('/users/me/stats') as { data: UserStats };
  return res.data;
};

export const getWordCloud = async (): Promise<WordCloudItem[]> => {
  const res = await request.get('/emotiontags/wordcloud') as { data: WordCloudItem[] };
  return res.data;
};

export const getUsedTags = async (): Promise<string[]> => {
  const res = await request.get('/emotiontags/used') as { data: string[] };
  return res.data;
};

export const getUserFavorites = async (): Promise<FavoriteDto[]> => {
  const res = await request.get('/favorites', {
    params: { page: 1, pageSize: 100 },
  }) as { data: { items: FavoriteDto[] } };
  return res.data.items;
};

export const getEmotionCurves = async (favoriteId: number): Promise<EmotionCurve[]> => {
  const res = await request.get(`/emotioncurves/${favoriteId}`) as { data: EmotionCurve[] };
  return res.data;
};