import request from '@/api'

export const getAdminReviewList = (params) => request.get('/api/reviews/admin', { params })
export const getReviewDetail = (favoriteId) => request.get(`/api/reviews/${favoriteId}/admin`)
export const deleteReview = (favoriteId) => request.delete(`/api/reviews/${favoriteId}/admin`)
