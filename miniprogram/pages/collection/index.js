import * as animeApi from '../../utils/anime'
import { checkAuth } from '../../utils/auth'

const app = getApp()

// 状态文字映射
const STATUS_TEXT = { 0: '想看', 1: '在看', 2: '看过' }

Page({
  data: {
    currentTab: 0,
    tabList: ['想看', '在看', '看过'],
    favoriteList: [],
    page: 1,
    pageSize: 20,
    total: 0,
    loading: false,
    hasMore: true,
    userTags: [],
    activeTag: '',
    activeTagIndex: 0,
    sortOptions: ['默认顺序', '评分最高', '评分最低'],
    sortIndex: 0,
    orderBy: null,
    isGuest: false
  },

  onLoad() {
    if (!checkAuth()) {
      this.setData({ isGuest: true })
      return
    }
    this.setData({ isGuest: false })
  },

  onShow() {
    if (!checkAuth()) {
      this.setData({ isGuest: true })
      return
    }
    const redirectTab = wx.getStorageSync('redirectTab')
    if (redirectTab !== '' && redirectTab !== undefined) {
      this.setData({ currentTab: redirectTab })
      wx.removeStorageSync('redirectTab')
    }
    this.loadUsedTags()
    if (app.globalData.needRefreshFavorites) {
      this.loadFavorites(true)
      app.globalData.needRefreshFavorites = false
    }
  },

  async loadUsedTags() {
    try {
      const tags = await animeApi.getUsedEmotionTags()
      // getUsedEmotionTags 返回 Result<List<string>>.Data，即 string[]，不是 { data: [] } 结构
      this.setData({ userTags: Array.isArray(tags) ? tags : [] })
    } catch (e) {
      // 标签加载失败不影响主流程
    }
  },

  async loadFavorites(reset = false) {
    if (reset) {
      this.setData({ page: 1, favoriteList: [], hasMore: true })
    }

    const { currentTab, page, pageSize, activeTag } = this.data
    this.setData({ loading: true })

    const params = { page, pageSize, status: currentTab }
    if (activeTag) params.tagName = activeTag
    if (this.data.orderBy) params.orderBy = this.data.orderBy

    try {
      const result = await animeApi.getFavoriteList(params)

      const newList = reset
        ? result.items
        : [...this.data.favoriteList, ...result.items]

      this.setData({
        favoriteList: newList,
        total: result.totalCount,
        hasMore: newList.length < result.totalCount,
        loading: false
      })
    } catch (e) {
      this.setData({ loading: false })
      wx.showToast({ title: '加载失败', icon: 'none' })
    }
  },

  selectTag(e) {
    const tag = e.currentTarget.dataset.tag
    const index = e.currentTarget.dataset.index
    if (tag === this.data.activeTag) return
    this.setData({ activeTag: tag, activeTagIndex: index, page: 1, favoriteList: [] })
    this.loadFavorites(true)
  },

  onSortChange(e) {
    const sortMap = [null, 'rating_desc', 'rating_asc']
    const idx = parseInt(e.detail.value)
    if (idx === this.data.sortIndex) return
    this.setData({
      sortIndex: idx,
      orderBy: sortMap[idx],
      page: 1,
      favoriteList: []
    })
    this.loadFavorites(true)
  },

  onTabChange(e) {
    const index = parseInt(e.currentTarget.dataset.index)
    if (index === this.data.currentTab) return
    this.setData({ currentTab: index, page: 1, favoriteList: [] })
    this.loadFavorites(true)
  },

  onScrollToLower() {
    if (this.data.loading || !this.data.hasMore) return
    this.setData({ page: this.data.page + 1 })
    this.loadFavorites(false)
  },

  onReachBottom() {
    this.onScrollToLower()
  },

  onLongPress(e) {
    const favoriteId = e.currentTarget.dataset.favoriteId
    const item = this.data.favoriteList.find(f => f.id === favoriteId)
    if (!item) return

    wx.showActionSheet({
      itemList: ['修改状态', '删除收藏'],
      success: (res) => {
        if (res.tapIndex === 0) {
          this.showStatusPicker(favoriteId, item.status)
        } else if (res.tapIndex === 1) {
          this.onDeleteFavorite(favoriteId)
        }
      }
    })
  },

  showStatusPicker(favoriteId, currentStatus) {
    wx.showActionSheet({
      itemList: ['想看', '在看', '看过'],
      success: (res) => {
        const newStatus = res.tapIndex
        if (newStatus !== currentStatus) {
          this.onChangeStatus(favoriteId, newStatus)
        }
      }
    })
  },

  async onChangeStatus(favoriteId, newStatus) {
    try {
      await animeApi.updateFavorite(favoriteId, { status: newStatus })
      wx.showToast({ title: '状态已更新', icon: 'success' })
      await this.loadFavorites(true)
    } catch (e) {
      wx.showToast({ title: e.message || '更新失败', icon: 'none' })
    }
  },

  onDeleteFavorite(favoriteId) {
    wx.showModal({
      title: '确认删除',
      content: '确定要删除该收藏吗？',
      success: async (res) => {
        if (res.confirm) {
          try {
            await animeApi.deleteFavorite(favoriteId)
            wx.showToast({ title: '已删除', icon: 'success' })
            await this.loadFavorites(true)
          } catch (e) {
            wx.showToast({ title: e.message || '删除失败', icon: 'none' })
          }
        }
      }
    })
  },

  goToDetail(e) {
    const animeId = e.currentTarget.dataset.animeId
    wx.navigateTo({ url: `/pages/anime-detail/index?id=${animeId}` })
  },

  rateStarInList(e) {
    const index = e.currentTarget.dataset.index
    const star = e.currentTarget.dataset.star
    const newRating = star * 2

    const currentItem = this.data.favoriteList[index]
    const finalRating = (currentItem.rating === newRating) ? null : newRating

    animeApi.updateFavorite(currentItem.id, { rating: finalRating }).then(() => {
      const listPath = `favoriteList[${index}].rating`
      this.setData({ [listPath]: finalRating })
      if (finalRating) {
        wx.showToast({ title: `评分 ${finalRating}/10`, icon: 'success' })
      } else {
        wx.showToast({ title: '已取消评分', icon: 'none' })
      }
    }).catch(err => {
      wx.showToast({ title: err.message || '评分失败', icon: 'none' })
    })
  }
})
