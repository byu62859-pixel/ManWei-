import request from '@/api'

export const getAnimeList = (params) => request.get('/api/anime', { params })
export const syncAnime = (bangumiId) => request.post(`/api/anime/Sync/${bangumiId}`)
export const createAnime = (data) => request.post('/api/anime', data)
export const updateAnime = (id, data) => request.put(`/api/anime/${id}`, data)
export const deleteAnime = (id) => request.delete(`/api/anime/${id}`)
export const batchDeleteAnime = (ids) => request.post('/api/anime/admin/batch-delete', { ids })
export const getAnimeWordCloud = (animeId) => request.get(`/api/EmotionTags/anime/${animeId}/wordcloud`)
