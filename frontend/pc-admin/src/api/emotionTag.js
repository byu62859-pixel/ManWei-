import request from '@/api'

export const getTagStats = (params) => request.get('/api/EmotionTags/stats', { params })
export const deleteTag = (id) => request.delete(`/api/EmotionTags/${id}`)
