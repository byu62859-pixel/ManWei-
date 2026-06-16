import { create } from 'zustand';
import request from '../../../services/request';
import type { FavoriteDto, FavoriteStatus, FavoriteUpdateDto, AnimeSearchResultItem, AddFavoriteParams } from '../types';

interface FavoritesState {
  list: FavoriteDto[];
  loading: boolean;
  counts: {
    all: number;
    wish: number;
    watching: number;
    watched: number;
  };
  pagination: {
    page: number;
    pageSize: number;
    total: number;
  };
  filter: {
    status: FavoriteStatus | null;
    orderBy: string;
  };
  addModalVisible: boolean;
  searchResults: AnimeSearchResultItem[];
  searchLoading: boolean;
  selectedItem: AnimeSearchResultItem | null;
  _lastSearchId: number;  // 内部字段，防止竞态，不暴露给组件
  fetchFavorites: () => Promise<void>;
  fetchCounts: () => Promise<void>;
  updateProgress: (id: number, progress: number) => Promise<void>;
  updateRating: (id: number, rating: number | null) => Promise<void>;
  updateStatus: (id: number, status: FavoriteStatus) => Promise<void>;
  deleteFavorite: (id: number) => Promise<void>;
  setFilter: (filter: Partial<FavoritesState['filter']>) => void;
  setPage: (page: number) => void;
  searchAnime: (keyword: string) => Promise<void>;
  addFavorite: (params: AddFavoriteParams) => Promise<{ code: number; message?: string }>;
  setAddModalVisible: (visible: boolean) => void;
  resetAddModal: () => void;
  setSelectedItem: (item: AnimeSearchResultItem | null) => void;
}

export const useFavoritesStore = create<FavoritesState>((set, get) => ({
  list: [],
  loading: false,
  counts: {
    all: 0,
    wish: 0,
    watching: 0,
    watched: 0,
  },
  pagination: {
    page: 1,
    pageSize: 20,
    total: 0,
  },
  filter: {
    status: null,
    orderBy: 'CreateTime',
  },
  addModalVisible: false,
  searchResults: [],
  searchLoading: false,
  selectedItem: null,
  _lastSearchId: 0,

  fetchFavorites: async () => {
    const { pagination, filter } = get();
    set({ loading: true });
    try {
      const params: Record<string, any> = {
        Page: pagination.page,
        PageSize: pagination.pageSize,
      };
      if (filter.status !== null) {
        params.Status = filter.status;
      }
      if (filter.orderBy) {
        params.OrderBy = filter.orderBy;
      }
      const res = await request.get('/favorites', { params }) as any;
      if (res.code === 200) {
        set({
          list: res.data.items,
          pagination: {
            ...pagination,
            total: res.data.totalCount,
          },
        });
      }
    } catch {
      // error
    } finally {
      set({ loading: false });
    }
  },

  fetchCounts: async () => {
    try {
      const res = await request.get('/favorites/counts') as any;
      if (res.code === 200) {
        set({
          counts: {
            all: res.data.all,
            wish: res.data.wish,
            watching: res.data.watching,
            watched: res.data.watched,
          },
        });
      }
    } catch {
      // error
    }
  },

  updateProgress: async (id: number, progress: number) => {
    const updates: FavoriteUpdateDto = { progress };
    const res = await request.put(`/favorites/${id}`, updates) as any;
    if (res.code === 200) {
      set((state) => ({
        list: state.list.map((item) =>
          item.id === id ? { ...item, progress } : item
        ),
      }));
    }
  },

  updateRating: async (id: number, rating: number | null) => {
    const updates: FavoriteUpdateDto = { rating };
    const res = await request.put(`/favorites/${id}`, updates) as any;
    if (res.code === 200) {
      set((state) => ({
        list: state.list.map((item) =>
          item.id === id ? { ...item, rating } : item
        ),
      }));
    }
  },

  updateStatus: async (id: number, status: FavoriteStatus) => {
    const updates: FavoriteUpdateDto = { status };
    const res = await request.put(`/favorites/${id}`, updates) as any;
    if (res.code === 200) {
      set((state) => ({
        list: state.list.map((item) =>
          item.id === id ? { ...item, status } : item
        ),
      }));
    }
  },

  deleteFavorite: async (id: number) => {
    const res = await request.delete(`/favorites/${id}`) as any;
    if (res.code === 200) {
      set((state) => ({
        list: state.list.filter((item) => item.id !== id),
        pagination: {
          ...state.pagination,
          total: state.pagination.total - 1,
        },
      }));
    }
  },

  setFilter: (filter) => {
    set((state) => ({
      filter: { ...state.filter, ...filter },
    }));
  },

  setPage: (page: number) => {
    set((state) => ({
      pagination: { ...state.pagination, page },
    }));
  },

  searchAnime: async (keyword: string) => {
    if (!keyword.trim()) {
      set({ searchResults: [] });
      return;
    }
    const requestId = Date.now();
    set({ searchLoading: true, _lastSearchId: requestId, searchResults: [] });
    try {
      const res = await request.get('/favorites/search-anime',
        { params: { keyword } }) as any;
      const currentId = get()._lastSearchId;
      if (res.code === 200 && currentId === requestId) {
        set({ searchResults: res.data });
      }
    } catch {
      // 静默处理
    } finally {
      const currentId = get()._lastSearchId;
      if (currentId === requestId) {
        set({ searchLoading: false });
      }
    }
  },

  addFavorite: async (params: AddFavoriteParams) => {
    try {
      const res = await request.post('/favorites/add', params) as any;
      if (res.code === 200) {
        const newItem: FavoriteDto = res.data;
        set((state) => ({
          list: [newItem, ...state.list],
          pagination: {
            ...state.pagination,
            total: state.pagination.total + 1,
          },
          counts: {
            ...state.counts,
            all: state.counts.all + 1,
            wish: state.counts.wish + 1,
          },
        }));
      }
      return { code: res.code, message: res.message };
    } catch {
      return { code: 500, message: '网络错误' };
    }
  },

  setAddModalVisible: (visible: boolean) => set({ addModalVisible: visible }),

  resetAddModal: () => set({
    addModalVisible: false,
    searchResults: [],
    searchLoading: false,
    selectedItem: null,
    _lastSearchId: 0,
  }),

  setSelectedItem: (item) => set({ selectedItem: item }),
}));