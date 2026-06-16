import { create } from 'zustand';
import type { UserStats, WordCloudItem, EmotionCurveWithAnime } from '../types/api';
import * as api from '../services/dataCenter';

interface DataCenterState {
  userStats: UserStats | null;
  wordCloudData: WordCloudItem[];
  emotionCurves: EmotionCurveWithAnime[];
  loading: boolean;
  error: string | null;

  fetchUserStats: () => Promise<void>;
  fetchWordCloud: () => Promise<void>;
  fetchEmotionCurves: () => Promise<void>;
  fetchAll: () => Promise<void>;
}

export const useDataCenterStore = create<DataCenterState>((set, get) => ({
  userStats: null,
  wordCloudData: [],
  emotionCurves: [],
  loading: false,
  error: null,

  fetchUserStats: async () => {
    try {
      const stats = await api.getUserStats();
      set({ userStats: stats });
    } catch (e) {
      set({ error: '获取用户统计失败' });
    }
  },

  fetchWordCloud: async () => {
    try {
      const data = await api.getWordCloud();
      set({ wordCloudData: data });
    } catch (e) {
      set({ error: '获取词云数据失败' });
    }
  },

  fetchEmotionCurves: async () => {
    set({ loading: true });
    try {
      const favorites = await api.getUserFavorites();
      const curvesPromises = favorites.map(async (fav) => {
        const curves = await api.getEmotionCurves(fav.id);
        return curves.map((c) => ({
          ...c,
          favoriteId: fav.id,
          animeName: fav.animeName,
          animeType: fav.animeType,
        }));
      });

      const allCurves = await Promise.all(curvesPromises);
      const flatCurves = allCurves.flat();
      set({ emotionCurves: flatCurves, loading: false });
    } catch (e) {
      set({ error: '获取情感曲线失败', loading: false });
    }
  },

  fetchAll: async () => {
    set({ loading: true, error: null });
    try {
      await Promise.all([get().fetchUserStats(), get().fetchWordCloud(), get().fetchEmotionCurves()]);
    } finally {
      set({ loading: false });
    }
  },
}));