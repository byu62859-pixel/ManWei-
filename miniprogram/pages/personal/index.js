import * as animeApi from '../../utils/anime'
import { checkAuth, clearToken, updateNickname } from '../../utils/auth'

Page({
  data: {
    isLoggedIn: false,
    nickname: '漫味用户',
    avatarText: '漫',
    wantCount: 0,
    watchingCount: 0,
    doneCount: 0,
    wordCloudData: [],
    maxCount: 0,
    loading: false,
    tooltip: null,
    // 昵称编辑
    editingNickname: false,
    nicknameInput: '',
    // 追番统计
    userStats: {
      totalEpisodes: 0,
      avgRating: '-',
      reviewCount: 0
    },
    // 最近观后感
    recentReviews: []
  },

  onLoad() {
    this.initPage()
  },

  onShow() {
    // 每次显示同步最新登录态
    if (checkAuth()) {
      this.setData({ isLoggedIn: true })
      this.loadUserInfo()
      this.loadFavoriteStats()
      this.loadWordCloud()
      this.loadUserStats()
      this.loadRecentReviews()
    } else {
      this.setData({ isLoggedIn: false })
    }
  },

  initPage() {
    const isLoggedIn = checkAuth()
    this.setData({ isLoggedIn })
    if (isLoggedIn) {
      this.loadUserInfo()
      this.loadFavoriteStats()
      this.loadWordCloud()
      this.loadUserStats()
      this.loadRecentReviews()
    }
  },

  loadUserInfo() {
    const userInfo = wx.getStorageSync('userInfo')
    if (userInfo && userInfo.nickName) {
      this.setData({
        nickname: userInfo.nickName,
        avatarText: userInfo.nickName.charAt(0)
      })
    }
  },

  async loadFavoriteStats() {
    try {
      const res0 = await animeApi.getFavoriteList({ status: 0, pageSize: 1 })
      const res1 = await animeApi.getFavoriteList({ status: 1, pageSize: 1 })
      const res2 = await animeApi.getFavoriteList({ status: 2, pageSize: 1 })
      this.setData({
        wantCount: res0.totalCount,
        watchingCount: res1.totalCount,
        doneCount: res2.totalCount
      })
    } catch (e) {
      // ignore
    }
  },

  async loadWordCloud() {
    this.setData({ loading: true })
    try {
      const res = await animeApi.getWordCloud()
      const maxCount = Math.max(...res.map(d => d.count), 1)
      const wordCloudData = res.map(d => ({
        ...d,
        style: this.getWordStyle(d.count, maxCount)
      }))
      this.setData({ wordCloudData, maxCount, loading: false })
    } catch (e) {
      this.setData({ wordCloudData: [], loading: false })
    }
  },

  getWordStyle(count, maxCount) {
    const minSize = 24
    const maxSize = 52
    const ratio = maxCount > 0 ? count / maxCount : 0
    const fontSize = Math.round(minSize + (maxSize - minSize) * ratio)
    const minOpacity = 0.5
    const maxOpacity = 1.0
    const opacity = minOpacity + (maxOpacity - minOpacity) * ratio
    // 6色气泡调色板，按词频映射
    const palette = [
      { r: 255, g: 107, b: 107 },  // 珊瑚红
      { r: 255, g: 174, b: 101 },  // 橙黄
      { r: 102, g: 209, b: 150 },  // 薄荷绿
      { r: 102, g: 176, b: 250 },  // 天蓝
      { r: 167, g: 139, b: 250 },  // 薰衣草紫
      { r: 250, g: 169, b: 230 }   // 粉色
    ]
    const colorIdx = Math.floor(ratio * (palette.length - 1))
    const c = palette[colorIdx]
    const color = `rgba(${c.r}, ${c.g}, ${c.b}, ${opacity})`
    const shadow = ratio > 0.5 ? `box-shadow: 0 4rpx 16rpx rgba(${c.r}, ${c.g}, ${c.b}, ${0.35});` : ''
    // 浮动动画：词频越大动画越慢、幅度越小
    const floatDuration = (2.5 + ratio * 1.5).toFixed(1)
    const floatDelay = (Math.random() * 2).toFixed(1)
    return `font-size: ${fontSize}rpx; color: ${color}; ${shadow} --float-duration:${floatDuration}s; --float-delay:${floatDelay}s;`
  },

  // 点击气泡显示次数提示
  onWordCloudTap(e) {
    const idx = e.currentTarget.dataset.idx
    const item = this.data.wordCloudData[idx]
    if (!item) return  // 无数据不弹

    const query = wx.createSelectorQuery().in(this)
    query.selectAll('.wordcloud-tag').boundingClientRect((rects) => {
      if (!rects || !rects[idx]) return
      const rect = rects[idx]
      const cardQuery = wx.createSelectorQuery().in(this)
      cardQuery.select('.wordcloud-card').boundingClientRect((cardRect) => {
        if (!cardRect) return
        const relativeTop = rect.top - cardRect.top
        const relativeLeft = rect.left + rect.width / 2 - cardRect.left
        this.setData({ tooltip: null })
        setTimeout(() => {
          this.setData({
            tooltip: {
              text: `${item.name} 出现 ${item.count} 次`,
              top: relativeTop,
              left: relativeLeft
            }
          })
          setTimeout(() => {
            if (this.data.tooltip) this.setData({ tooltip: null })
          }, 2000)
        }, 10)
      }).exec()
    }).exec()
  },

  // 点击空白区域关闭提示
  onWordCloudAreaTap() {
    if (this.data.tooltip) this.setData({ tooltip: null })
  },

  async loadUserStats() {
    try {
      const res = await animeApi.getUserStats()
      this.setData({
        userStats: {
          totalEpisodes: res.totalEpisodes || 0,
          avgRating: res.avgRating ? res.avgRating.toFixed(1) : '-',
          reviewCount: res.reviewCount || 0
        }
      })
    } catch (e) {
      this.setData({
        userStats: { totalEpisodes: 0, avgRating: '-', reviewCount: 0 }
      })
    }
  },

  async loadRecentReviews() {
    try {
      const res = await animeApi.getReviewFeed({ page: 1, pageSize: 2 })
      const recentReviews = (res.items || []).map(item => {
        const text = (item.content || '').replace(/\r?\n/g, ' ')
        return {
          favoriteId: item.favoriteId,
          animeName: item.animeName,
          contentSummary: text.length > 40 ? text.substring(0, 40) + '...' : text
        }
      })
      this.setData({ recentReviews })
    } catch (e) {
      this.setData({ recentReviews: [] })
    }
  },

  goToReviews() {
    wx.switchTab({ url: '/pages/reviews/index' })
  },

  goToCollection(e) {
    const tab = e.currentTarget.dataset.tab
    wx.setStorageSync('redirectTab', parseInt(tab))
    wx.switchTab({ url: '/pages/collection/index' })
  },

  // 昵称编辑
  onEditNickname() {
    const userInfo = wx.getStorageSync('userInfo') || {}
    this.setData({ editingNickname: true, nicknameInput: userInfo.nickName || '' })
  },

  onCancelEditNickname() {
    this.setData({ editingNickname: false })
  },

  onNicknameInput(e) {
    this.setData({ nicknameInput: e.detail.value })
  },

  async onConfirmNickname() {
    const nickName = this.data.nicknameInput.trim()
    if (!nickName) {
      wx.showToast({ title: '昵称不能为空', icon: 'none' })
      return
    }
    try {
      await updateNickname({ nickName })
      wx.setStorageSync('userInfo', { ...wx.getStorageSync('userInfo'), nickName })
      this.setData({
        nickname: nickName,
        avatarText: nickName.charAt(0),
        editingNickname: false
      })
      wx.showToast({ title: '修改成功', icon: 'success' })
    } catch (e) {
      wx.showToast({ title: e.message || '修改失败', icon: 'none' })
    }
  },

  // 退出登录
  onLogout() {
    wx.showModal({
      title: '确认退出',
      content: '确定要退出登录吗？',
      success: (res) => {
        if (res.confirm) {
          clearToken()
          wx.removeStorageSync('userInfo')
          wx.removeStorageSync('userId')
          wx.removeStorageSync('ai_chat_history')
          this.setData({
            isLoggedIn: false,
            nickname: '漫味用户',
            avatarText: '漫',
            wantCount: 0,
            watchingCount: 0,
            doneCount: 0,
            wordCloudData: [],
            userStats: { totalEpisodes: 0, avgRating: '-', reviewCount: 0 },
            recentReviews: []
          })
          wx.switchTab({ url: '/pages/index/index' })
        }
      }
    })
  },

  goLogin() {
    wx.navigateTo({ url: '/pages/login/login' })
  },

  goToAiChat() {
    if (!checkAuth()) {
      wx.navigateTo({ url: '/pages/login/login' })
      return
    }
    wx.navigateTo({ url: '/pages/ai-chat/index' })
  }
})
