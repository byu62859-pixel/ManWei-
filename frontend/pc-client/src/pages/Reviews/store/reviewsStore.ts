import { create } from 'zustand';
import request from '../../../services/request';
import type { ReviewDto } from '../types';

interface ReviewsState {
  list: ReviewDto[];
  loading: boolean;
  pagination: {
    page: number;
    pageSize: number;
    total: number;
  };
  orderBy: string;
  fetchReviews: () => Promise<void>;
  saveReview: (favoriteId: number, content: string) => Promise<void>;
  deleteReview: (favoriteId: number) => Promise<void>;
  setPage: (page: number) => void;
  setOrderBy: (orderBy: string) => void;
}

export const useReviewsStore = create<ReviewsState>((set, get) => ({
  list: [],
  loading: false,
  pagination: {
    page: 1,
    pageSize: 20,
    total: 0,
  },
  orderBy: 'CreateTime',

  fetchReviews: async () => {
    const { pagination, orderBy } = get();
    set({ loading: true });
    try {
      const params: Record<string, any> = {
        page: pagination.page,
        pageSize: pagination.pageSize,
      };
      if (orderBy) {
        params.orderBy = orderBy;
      }
      const res = await request.get('/reviews', { params }) as any;
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

  saveReview: async (favoriteId: number, content: string) => {
    const res = await request.post('/reviews', { favoriteId, content }) as any;
    if (res.code === 200) {
      // Refresh list after save
      get().fetchReviews();
    }
  },

  deleteReview: async (favoriteId: number) => {
    const res = await request.delete(`/reviews/${favoriteId}`) as any;
    if (res.code === 200) {
      set((state) => ({
        list: state.list.filter((item) => item.favoriteId !== favoriteId),
        pagination: {
          ...state.pagination,
          total: state.pagination.total - 1,
        },
      }));
    }
  },

  setPage: (page: number) => {
    set((state) => ({
      pagination: { ...state.pagination, page },
    }));
  },

  setOrderBy: (orderBy: string) => {
    set({ orderBy });
  },
}));