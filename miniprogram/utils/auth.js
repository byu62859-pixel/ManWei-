import { post, put } from './api'

// 微信小程序登录
export function loginByWechat(code) {
  return post('/api/Auth/wx-login', { code })
}

// 保存 token
export function setToken(token) {
  wx.setStorageSync('token', token)
}

// 获取 token
export function getToken() {
  return wx.getStorageSync('token')
}

// 清除 token
export function clearToken() {
  wx.removeStorageSync('token')
}

// 检查登录态
export function checkAuth() {
  return !!getToken()
}

// 修改昵称
export function updateNickname(data) {
  return put('/api/Users/me/nickname', data)
}
