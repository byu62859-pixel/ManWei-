import * as animeApi from '../../utils/anime'
import { checkAuth } from '../../utils/auth'

const app = getApp()

// 预置情感标签（未登录/未收藏时前端硬编码）
const PRESET_TAGS = [
  { id: 0, name: '泪崩', isPreset: true, animeId: null, createTime: null },
  { id: 0, name: '热血', isPreset: true, animeId: null, createTime: null },
  { id: 0, name: '治愈', isPreset: true, animeId: null, createTime: null },
  { id: 0, name: '致郁', isPreset: true, animeId: null, createTime: null },
  { id: 0, name: '笑死', isPreset: true, animeId: null, createTime: null },
  { id: 0, name: '神作', isPreset: true, animeId: null, createTime: null }
]

// 统一游客拦截
const requireAuth = () => {
  if (!checkAuth()) {
    wx.showModal({
      title: '提示',
      content: '登录后可进行此操作，是否前往登录？',
      confirmText: '去登录',
      success: (res) => {
        if (res.confirm) wx.navigateTo({ url: '/pages/login/login' })
      }
    })
    return false
  }
  return true
}

Page({
  data: {
    // 页面状态
    animeId: null,
    anime: null,
    currentTab: 'tags',
    loading: false,
    loadingTags: false,
    loadingCurve: false,
    summaryExpanded: false,

    // 收藏相关
    favoriteInfo: {
      isFavorited: false,
      favoriteId: null,
      status: null,
      statusText: '',
      rating: null
    },
    isEditingStatus: false,
    statusList: [
      { label: '想看', value: 0 },
      { label: '在看', value: 1 },
      { label: '看过', value: 2 }
    ],

    // 标签相关
    tagList: [],
    newTagName: '',

    // 情感曲线相关
    curveData: [],
    selectedPoint: null,
    chartXPositions: [],
    inputEpisode: 1,
    inputEmotionLevel: 3,
    emotionLevels: [
      { value: 1, emoji: '😢', label: '悲伤' },
      { value: 2, emoji: '😐', label: '平淡' },
      { value: 3, emoji: '😊', label: '普通' },
      { value: 4, emoji: '😭', label: '感动' },
      { value: 5, emoji: '🔥', label: '热血' }
    ],

    // 观后感相关
    reviewContent: '',

    // Tab 配置
    tabs: [
      { key: 'tags', label: '情感标签' },
      { key: 'curve', label: '情绪曲线' },
      { key: 'review', label: '观后感' }
    ]
  },

  onLoad(options) {
    const animeId = parseInt(options.id)
    if (!animeId) {
      wx.showToast({ title: '参数错误', icon: 'none' })
      wx.navigateBack()
      return
    }
    this.setData({ animeId })

    if (options.tab === 'review') {
      this.setData({ currentTab: 'review' })
    }

    this.loadAnimeDetail()
  },

  onShow() {
    if (this.data.animeId) {
      this.loadFavoriteStatus()
    }
  },

  onUnload() {
    if (this.data.ecChart) {
      this.data.ecChart.dispose()
    }
  },

  // ========== 数据加载 ==========

  async loadAnimeDetail() {
    this.setData({ loading: true })
    try {
      const anime = await animeApi.getAnimeDetail(this.data.animeId)
      this.setData({ anime, loading: false })
      this.loadFavoriteStatus()
    } catch (e) {
      this.setData({ loading: false })
      wx.showToast({ title: '加载失败', icon: 'none' })
    }
  },

  async loadFavoriteStatus() {
    const isLoggedIn = checkAuth()
    if (!isLoggedIn) {
      this.setData({
        favoriteInfo: { isFavorited: false, favoriteId: null, status: null, statusText: '', rating: null },
        tagList: PRESET_TAGS
      })
      return
    }

    try {
      const res = await animeApi.checkFavorite(this.data.animeId)
      this.setData({
        favoriteInfo: {
          isFavorited: res.isFavorited,
          favoriteId: res.favoriteId || null,
          status: res.status,
          statusText: res.statusText || '',
          rating: res.rating || null
        }
      })

      if (res.isFavorited) {
        this.loadTags()
        this.loadTabData()
      } else {
        this.setData({ tagList: PRESET_TAGS })
      }
    } catch (e) {
      this.setData({
        favoriteInfo: { isFavorited: false, favoriteId: null, status: null, statusText: '', rating: null },
        tagList: PRESET_TAGS
      })
    }
  },

  async loadTags() {
    this.setData({ loadingTags: true })
    try {
      const tags = await animeApi.getEmotionTags(this.data.animeId)
      const customTags = tags.filter(t => !t.isPreset)
      const mergedTags = [...PRESET_TAGS.map(t => ({ ...t })), ...customTags]
      this.setData({ tagList: mergedTags, loadingTags: false })
    } catch (e) {
      this.setData({ loadingTags: false, tagList: PRESET_TAGS })
    }
  },

  async loadTabData() {
    const { currentTab, favoriteInfo } = this.data
    if (!favoriteInfo.favoriteId) return

    switch (currentTab) {
      case 'tags': await this.loadTags(); break
      case 'curve': await this.loadEmotionCurve(); break
      case 'review': await this.loadReview(); break
    }
  },

  async loadEmotionCurve() {
    this.setData({ loadingCurve: true })
    try {
      const curves = await animeApi.getEmotionCurves(this.data.favoriteInfo.favoriteId)
      this.setData({ curveData: curves, loadingCurve: false })
      this.drawChart()
    } catch (e) {
      this.setData({ curveData: [], loadingCurve: false })
      this.drawChart()
    }
  },

  async loadReview() {
    try {
      const review = await animeApi.getReview(this.data.favoriteInfo.favoriteId)
      this.setData({ reviewContent: review?.content || '' })
    } catch (e) {
      this.setData({ reviewContent: '' })
    }
  },

  // ========== Canvas 折线图绘制 ==========

  drawChart() {
    const query = wx.createSelectorQuery().in(this)
    query.select('#emotionChart')
      .fields({ node: true, size: true })
      .exec((res) => {
        const canvas = res[0].node
        if (!canvas) return

        const ctx = canvas.getContext('2d')
        const width = res[0].width
        const height = res[0].height
        const dpr = wx.getWindowInfo().pixelRatio
        canvas.width = width * dpr
        canvas.height = height * dpr
        ctx.scale(dpr, dpr)

        const data = this.data.curveData
        if (!data || data.length === 0) return

        const padding = { top: 30, right: 20, bottom: 36, left: 20 }
        const chartWidth = width - padding.left - padding.right
        const chartHeight = height - padding.top - padding.bottom
        const chartBottom = padding.top + chartHeight

        const showAllLabels = data.length <= 10
        const labelIndices = new Set()
        if (showAllLabels) {
          for (let i = 0; i < data.length; i++) labelIndices.add(i)
        } else {
          labelIndices.add(0)
          labelIndices.add(data.length - 1)
          const step = Math.ceil(data.length / 6)
          for (let i = step; i < data.length - 1; i += step) labelIndices.add(i)
        }

        const xPositions = []
        const points = []

        ctx.clearRect(0, 0, width, height)

        ctx.beginPath()
        ctx.strokeStyle = '#667eea'
        ctx.lineWidth = 2
        data.forEach((item, index) => {
          const x = padding.left + (index / (data.length - 1 || 1)) * chartWidth
          const y = padding.top + (1 - (item.emotionLevel - 1) / 4) * chartHeight
          if (index === 0) ctx.moveTo(x, y)
          else ctx.lineTo(x, y)
          xPositions.push(x)
          points.push({ x, y, item })
        })
        ctx.stroke()

        this.setData({ chartXPositions: xPositions })

        points.forEach(({ x, y, item }, index) => {
          ctx.beginPath()
          ctx.arc(x, y, 5, 0, Math.PI * 2)
          ctx.fillStyle = '#fff'
          ctx.fill()
          ctx.beginPath()
          ctx.arc(x, y, 3.5, 0, Math.PI * 2)
          ctx.fillStyle = '#667eea'
          ctx.fill()

          if (labelIndices.has(index)) {
            ctx.fillStyle = '#1A6B4A'
            ctx.font = 'bold 11px sans-serif'
            ctx.textAlign = 'center'
            ctx.fillText(String(item.emotionLevel), x, y - 10)
          }
          if (labelIndices.has(index)) {
            ctx.fillStyle = '#999'
            ctx.font = '10px sans-serif'
            ctx.textAlign = 'center'
            ctx.fillText(`第${item.episode}集`, x, chartBottom + 16)
          }
        })
      })
  },

  onChartTap(e) {
    const xPositions = this.data.chartXPositions
    if (!xPositions || xPositions.length === 0) return
    const touchX = e.touches[0]?.x
    if (touchX == null) return

    let closestIdx = 0
    let minDist = Infinity
    xPositions.forEach((px, i) => {
      const dist = Math.abs(px - touchX)
      if (dist < minDist) { minDist = dist; closestIdx = i }
    })

    if (minDist < 30) {
      this.setData({ selectedPoint: this.data.curveData[closestIdx] })
    } else {
      this.setData({ selectedPoint: null })
    }
  },

  // ========== Tab 切换 ==========

  onSwitchTab(e) {
    const tab = e.currentTarget.dataset.tab
    if (tab === this.data.currentTab) return
    this.setData({ currentTab: tab })
    this.loadTabData()
  },

  // ========== 收藏操作 ==========

  async onAddFavorite() {
    if (!requireAuth()) return

    try {
      await animeApi.addFavorite(this.data.animeId)
      wx.showToast({ title: '收藏成功', icon: 'success' })
      app.globalData.needRefreshFavorites = true
      this.loadFavoriteStatus()
    } catch (e) {
      wx.showToast({ title: e.message || '收藏失败', icon: 'none' })
    }
  },

  onEditStatus() {
    if (!requireAuth()) return
    if (!this.data.favoriteInfo.isFavorited) {
      wx.showToast({ title: '请先添加收藏', icon: 'none' })
      return
    }
    this.setData({ isEditingStatus: true })
  },

  onCancelEditStatus() {
    this.setData({ isEditingStatus: false })
  },

  async onSelectStatus(e) {
    const status = e.currentTarget.dataset.status
    try {
      await animeApi.updateFavorite(this.data.favoriteInfo.favoriteId, { status })
      this.setData({
        isEditingStatus: false,
        'favoriteInfo.status': status,
        'favoriteInfo.statusText': this.getStatusText(status)
      })
      app.globalData.needRefreshFavorites = true
      wx.showToast({ title: '状态已更新', icon: 'success' })
    } catch (e) {
      wx.showToast({ title: e.message || '更新失败', icon: 'none' })
    }
  },

  getStatusText(status) {
    const map = { 0: '想看', 1: '在看', 2: '看过' }
    return map[status] ?? ''
  },

  // ========== 评分操作 ==========

  rateStar(e) {
    if (!requireAuth()) return
    const star = e.currentTarget.dataset.star
    const newRating = star * 2
    const { favoriteId, rating: currentRating } = this.data.favoriteInfo
    const finalRating = (currentRating === newRating) ? null : newRating

    animeApi.updateFavorite(favoriteId, { rating: finalRating }).then(() => {
      this.setData({ 'favoriteInfo.rating': finalRating })
      if (finalRating) {
        wx.showToast({ title: `评分 ${finalRating}/10`, icon: 'success' })
      } else {
        wx.showToast({ title: '已取消评分', icon: 'none' })
      }
      getApp().globalData.needRefreshFavorites = true
    }).catch(err => {
      wx.showToast({ title: err.message || '评分失败', icon: 'none' })
    })
  },

  onToggleSummary() {
    this.setData({ summaryExpanded: !this.data.summaryExpanded })
  },

  // ========== 标签操作 ==========

  onTagInput(e) {
    this.setData({ newTagName: e.detail.value })
  },

  async onAddTag() {
    if (!requireAuth()) return
    const { newTagName, animeId, favoriteInfo } = this.data
    if (!favoriteInfo.isFavorited) {
      wx.showToast({ title: '请先添加收藏', icon: 'none' })
      return
    }
    if (!newTagName.trim()) {
      wx.showToast({ title: '请输入标签名', icon: 'none' })
      return
    }
    try {
      await animeApi.createEmotionTag({ name: newTagName.trim(), animeId })
      wx.showToast({ title: '添加成功', icon: 'success' })
      app.globalData.needRefreshFavorites = true
      this.setData({ newTagName: '' })
      this.loadTags()
    } catch (e) {
      wx.showToast({ title: e.message || '添加失败', icon: 'none' })
    }
  },

  async onDeleteTag(e) {
    if (!requireAuth()) return
    const tagId = e.currentTarget.dataset.id
    wx.showModal({
      title: '确认删除',
      content: '确定要删除该标签吗？',
      success: async (res) => {
        if (res.confirm) {
          try {
            await animeApi.deleteEmotionTag(tagId)
            wx.showToast({ title: '已删除', icon: 'success' })
            app.globalData.needRefreshFavorites = true
            this.loadTags()
          } catch (e) {
            wx.showToast({ title: e.message || '删除失败', icon: 'none' })
          }
        }
      }
    })
  },

  // ========== 情感曲线操作 ==========

  onEpisodeInput(e) {
    this.setData({ inputEpisode: e.detail.value })
  },

  onSelectEmotion(e) {
    const level = parseInt(e.currentTarget.dataset.level)
    this.setData({ inputEmotionLevel: level })
  },

  async onSaveEmotionCurve() {
    if (!requireAuth()) return
    const { favoriteInfo, inputEpisode, inputEmotionLevel } = this.data
    if (!favoriteInfo.isFavorited) {
      wx.showToast({ title: '请先添加收藏', icon: 'none' })
      return
    }
    const episode = parseInt(inputEpisode) || 1
    try {
      await animeApi.upsertEmotionCurve({
        favoriteId: favoriteInfo.favoriteId,
        episode,
        emotionLevel: inputEmotionLevel
      })
      wx.showToast({ title: '保存成功', icon: 'success' })
      await this.loadEmotionCurve()
      this.drawChart()
    } catch (e) {
      wx.showToast({ title: e.message || '保存失败', icon: 'none' })
    }
  },

  // ========== 观后感操作 ==========

  onReviewInput(e) {
    this.setData({ reviewContent: e.detail.value })
  },

  async onSaveReview() {
    if (!requireAuth()) return
    const { favoriteInfo, reviewContent } = this.data
    if (!favoriteInfo.isFavorited) {
      wx.showToast({ title: '请先添加收藏', icon: 'none' })
      return
    }
    try {
      await animeApi.upsertReview({
        favoriteId: favoriteInfo.favoriteId,
        content: reviewContent
      })
      wx.showToast({ title: '保存成功', icon: 'success' })
      app.globalData.needRefreshFavorites = true
    } catch (e) {
      wx.showToast({ title: e.message || '保存失败', icon: 'none' })
    }
  }
})
