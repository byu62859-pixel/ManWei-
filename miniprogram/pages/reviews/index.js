import * as animeApi from '../../utils/anime'
import { checkAuth } from '../../utils/auth'

// 相对时间格式化工具（纯 JS，无 dayjs 依赖）
function formatRelativeTime(dateString) {
  if (!dateString) return ''
  const date = new Date(dateString)
  const now = new Date()
  const diffMs = now - date
  const diffSec = Math.floor(diffMs / 1000)
  const diffMin = Math.floor(diffSec / 60)
  const diffHour = Math.floor(diffMin / 60)
  const diffDay = Math.floor(diffHour / 24)
  const diffWeek = Math.floor(diffDay / 7)
  const diffMonth = Math.floor(diffDay / 30)
  const diffYear = Math.floor(diffDay / 365)

  if (diffSec < 60) return '刚刚'
  if (diffMin < 60) return `${diffMin}分钟前`
  if (diffHour < 24) return `${diffHour}小时前`
  if (diffDay < 7) return `${diffDay}天前`
  if (diffWeek < 4) return `${diffWeek}周前`
  if (diffMonth < 12) return `${diffMonth}个月前`
  return `${diffYear}年前`
}

Page({
  data: {
    reviewList: [],
    page: 1,
    pageSize: 10,
    total: 0,
    loading: false,
    hasMore: true
  },

  onLoad() {
    if (!checkAuth()) {
      wx.navigateTo({ url: '/pages/login/login' })
      return
    }
  },

  onShow() {
    if (!checkAuth()) return
    this.loadReviews(true)
  },

  // 下拉刷新
  onPullDownRefresh() {
    this.loadReviews(true).finally(() => wx.stopPullDownRefresh())
  },

  // 上拉翻页
  onReachBottom() {
    if (this.data.loading || !this.data.hasMore) return
    this.setData({ page: this.data.page + 1 })
    this.loadReviews(false)
  },

  async loadReviews(reset = false) {
    if (reset) {
      this.setData({ page: 1, reviewList: [], hasMore: true })
    }

    const { page, pageSize } = this.data
    this.setData({ loading: true })

    try {
      const result = await animeApi.getReviewFeed({ page, pageSize })

      // 数据预处理：格式化时间戳，WXML 无法直接调用 JS 函数
      const processedItems = result.items.map(item => ({
        ...item,
        displayTime: formatRelativeTime(item.updatedAt)
      }))

      const newList = reset
        ? processedItems
        : [...this.data.reviewList, ...processedItems]

      this.setData({
        reviewList: newList,
        total: result.totalCount,
        hasMore: newList.length < result.totalCount,
        loading: false
      })
    } catch (e) {
      this.setData({ loading: false })
      wx.showToast({ title: '加载失败', icon: 'none' })
    }
  },

  // 点击卡片跳转，带 tab=review 参数
  goToDetail(e) {
    const animeId = e.currentTarget.dataset.animeId
    wx.navigateTo({
      url: `/pages/anime-detail/index?id=${animeId}&tab=review`
    })
  }
})