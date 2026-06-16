App({
  globalData: {
    userId: null,
    token: null,
    needRefreshFavorites: false
  },

  onLaunch() {
    const token = wx.getStorageSync('token')
    if (token) {
      this.globalData.token = token
    }
  }
})
