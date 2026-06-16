import request from './request';
import type { UserProfile, UserStats } from '../types/api';

export const getMe = async (): Promise<UserProfile> => {
  const res = await request.get('/users/me') as { data: UserProfile };
  return res.data;
};

export const updateNickname = async (nickName: string): Promise<UserProfile> => {
  const res = await request.put('/users/me/nickname', { NickName: nickName }) as { data: UserProfile };
  return res.data;
};

export const uploadAvatar = async (file: File): Promise<UserProfile> => {
  const formData = new FormData();
  formData.append('file', file);

  const res = await request.post('/users/me/avatar', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  }) as { data: UserProfile };
  return res.data;
};

export const getMyStats = async (): Promise<UserStats> => {
  const res = await request.get('/users/me/stats') as { data: UserStats };
  return res.data;
};
