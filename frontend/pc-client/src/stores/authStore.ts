import { create } from 'zustand';
import request from '../services/request';
import type { UserProfile } from '../types/api';

interface LoginData {
  token: string;
  userId: number;
  nickName: string;
  role: string;
}

interface AuthState {
  token: string | null;
  userInfo: UserProfile | null;
  isLoggedIn: boolean;
  login: (userName: string, password: string) => Promise<void>;
  fetchMe: () => Promise<UserProfile | null>;
  updateUserInfo: (userInfo: UserProfile) => void;
  logout: () => void;
}

const USER_INFO_KEY = 'mw_user_info';

const loadUserInfo = (): UserProfile | null => {
  const raw = localStorage.getItem(USER_INFO_KEY);
  if (!raw) return null;

  try {
    return JSON.parse(raw) as UserProfile;
  } catch {
    localStorage.removeItem(USER_INFO_KEY);
    return null;
  }
};

const persistUserInfo = (userInfo: UserProfile | null) => {
  if (userInfo) {
    localStorage.setItem(USER_INFO_KEY, JSON.stringify(userInfo));
  } else {
    localStorage.removeItem(USER_INFO_KEY);
  }
};

export const useAuthStore = create<AuthState>((set) => ({
  token: localStorage.getItem('mw_token'),
  userInfo: loadUserInfo(),
  isLoggedIn: !!localStorage.getItem('mw_token'),

  login: async (userName: string, password: string) => {
    const res: any = await request.post('/auth/login', { userName, password });
    if (res.code === 200) {
      const { token } = res.data as LoginData;
      localStorage.setItem('mw_token', token);
      set({ token, isLoggedIn: true });

      const meRes = await request.get('/users/me') as { data: UserProfile };
      persistUserInfo(meRes.data);
      set({ userInfo: meRes.data });
    } else {
      throw new Error(res.message || '登录失败');
    }
  },

  fetchMe: async () => {
    if (!localStorage.getItem('mw_token')) return null;

    const res = await request.get('/users/me') as { data: UserProfile };
    persistUserInfo(res.data);
    set({ userInfo: res.data, isLoggedIn: true });
    return res.data;
  },

  updateUserInfo: (userInfo) => {
    persistUserInfo(userInfo);
    set({ userInfo });
  },

  logout: () => {
    localStorage.removeItem('mw_token');
    persistUserInfo(null);
    set({ token: null, userInfo: null, isLoggedIn: false });
  },
}));
