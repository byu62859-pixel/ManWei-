import { getAnimeList } from '../../utils/anime'

Page({
  data: {
    keyword: '',
    currentType: '',
    animeList: [],
    page: 1,
    pageSize: 20,
    total: 0,
    loading: false,
    hasMore: true,
    typeList: [
      { label: '全部', value: '' },
      { label: 'TV', value: 'TV' },
      { label: '剧场版', value: '剧场版' },
      { label: 'OVA', value: 'OVA' }
    ]
  },

  onLoad() {
    this.loadAnime(true)
  },

  async loadAnime(reset = false) {
    const { keyword, currentType, pageSize } = this.data

    if (reset) {
      this.setData({ page: 1, animeList: [], hasMore: true })
    }

    this.setData({ loading: true })

    try {
      const params = {
        page: reset ? 1 : this.data.page,
        pageSize
      }
      if (keyword) params.keyword = keyword
      if (currentType) params.type = currentType

      const result = await getAnimeList(params)

      const newList = reset ? result.items : [...this.data.animeList, ...result.items]
      const hasMore = newList.length < result.totalCount

      this.setData({
        animeList: newList,
        total: result.totalCount,
        hasMore,
        loading: false
      })
    } catch (e) {
      this.setData({ loading: false })
      wx.showToast({ title: '加载失败', icon: 'none' })
    }
  },

  onInput(e) {
    this._keyword = e.detail.value
  },

  onSearch() {
    const keyword = this._keyword || ''
    this.setData({ keyword })
    this.loadAnime(true)
  },

  onClear() {
    this._keyword = ''
    this.setData({ keyword: '' })
    this.loadAnime(true)
  },

  onTypeChange(e) {
    const type = e.currentTarget.dataset.type
    this.setData({ currentType: type, keyword: '' })
    this.loadAnime(true)
  },

  onLoadMore() {
    if (this.data.loading || !this.data.hasMore) return
    this.setData({ page: this.data.page + 1 })
    this.loadAnime(false)
  },

  onReachBottom() {
    this.onLoadMore()
  },

  goDetail(e) {
    const id = e.currentTarget.dataset.id
    wx.navigateTo({ url: `/pages/anime-detail/index?id=${id}` })
  }
})
