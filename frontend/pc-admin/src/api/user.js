import request from '@/api'

export const getUserList = (params) => request.get('/api/Users', { params })
export const updateUserStatus = (id, data) => request.put(`/api/Users/${id}/status`, data)
export const deleteUser = (id) => request.delete(`/api/Users/${id}`)
