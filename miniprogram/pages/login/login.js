import { loginByWechat, setToken } from '../../utils/auth'
import { post } from '../../utils/api'

Page({
  data: {
    account: '',
    password: '',
    showPassword: false
  },
  onLoad() {},

  async handleLogin() {
    wx.showLoading({ title: '登录中...' })
    try {
      const { code } = await wx.login()
      const res = await loginByWechat(code)
      setToken(res.token)
      // 持久化用户信息
      wx.setStorageSync('userInfo', { nickName: res.nickName || '漫味用户' })
      wx.setStorageSync('userId', res.userId)
      wx.showToast({ title: '登录成功' })
      setTimeout(() => {
        wx.switchTab({ url: '/pages/index/index' })
      }, 1000)
    } catch (e) {
      wx.showToast({ title: '登录失败', icon: 'none' })
    } finally {
      wx.hideLoading()
    }
  },

  onAccountInput: function(e) {
    this.setData({ account: e.detail.value })
  },

  onPasswordInput: function(e) {
    this.setData({ password: e.detail.value })
  },

  togglePassword: function() {
    this.setData({ showPassword: !this.data.showPassword })
  },

  handleAccountLogin: function() {
    const { account, password } = this.data
    if (!account) {
      wx.showToast({ title: '请输入账号', icon: 'none' })
      return
    }
    if (!password) {
      wx.showToast({ title: '请输入密码', icon: 'none' })
      return
    }

    wx.showLoading({ title: '登录中...' })

    post('/api/Auth/login', { userName: account, password }).then(res => {
      wx.hideLoading()
      setToken(res.token)
      wx.setStorageSync('userInfo', { nickName: res.nickName || '漫味用户' })
      wx.setStorageSync('userId', res.userId)
      wx.showToast({ title: '登录成功', icon: 'success' })
      setTimeout(() => wx.switchTab({ url: '/pages/index/index' }), 1000)
    }).catch(err => {
      wx.hideLoading()
    })
  },

  handleRegister: function() {
    const { account, password } = this.data
    if (!account) {
      wx.showToast({ title: '请输入账号', icon: 'none' })
      return
    }
    if (!password) {
      wx.showToast({ title: '请输入密码', icon: 'none' })
      return
    }

    wx.showLoading({ title: '注册中...' })

    post('/api/Auth/register', { userName: account, password }).then(() => {
      wx.hideLoading()
      wx.showToast({ title: '注册成功，请登录', icon: 'success' })
      this.setData({ password: '' })
    }).catch(err => {
      wx.hideLoading()
    })
  }
})
