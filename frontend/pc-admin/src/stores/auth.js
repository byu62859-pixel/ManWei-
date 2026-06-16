import { defineStore } from 'pinia'
import request from '@/api'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('token') || '',
    userId: localStorage.getItem('userId') || null,
    nickName: localStorage.getItem('nickName') || '',
    role: localStorage.getItem('role') || ''
  }),

  getters: {
    isAuthenticated: () => !!localStorage.getItem('token'),
    isAdmin: () => localStorage.getItem('role') === 'Admin'
  },

  actions: {
    async login(username, password) {
      const data = await request.post('/api/Auth/login', { username, password })
      this.token = data.token
      this.userId = data.userId
      this.nickName = data.nickName
      this.role = data.role

      localStorage.setItem('token', data.token)
      localStorage.setItem('userId', data.userId)
      localStorage.setItem('nickName', data.nickName)
      localStorage.setItem('role', data.role)
    },

    logout() {
      this.token = ''
      this.userId = null
      this.nickName = ''
      this.role = ''

      localStorage.removeItem('token')
      localStorage.removeItem('userId')
      localStorage.removeItem('nickName')
      localStorage.removeItem('role')
    }
  }
})
