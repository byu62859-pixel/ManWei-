import request from '@/api'

export const getStats = () => request.get('/api/Dashboard/stats')
export const getTodayOverview = () => request.get('/api/Dashboard/today-overview')
export const getUserGrowth = (days) => request.get('/api/Dashboard/user-growth', { params: { days } })
export const getAnimeRank = (top) => request.get('/api/Dashboard/anime-rank', { params: { top } })
export const getTagRank = () => request.get('/api/Dashboard/tag-rank')
