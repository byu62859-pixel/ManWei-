const BASE_URL = 'http://localhost:5150'

function request(url, method, data, timeout = 10000) {
  const token = wx.getStorageSync('token')

  return new Promise((resolve, reject) => {
    wx.request({
      url: BASE_URL + url,
      method,
      data,
      timeout,
      header: token ? { Authorization: `Bearer ${token}` } : {},
      success: (res) => {
        if (res.data.code === 200) {
          resolve(res.data.data)
        } else {
          wx.showToast({ title: res.data.message || '请求失败', icon: 'none' })
          reject(new Error(res.data.message))
        }
      },
      fail: (err) => {
        wx.showToast({ title: '网络错误', icon: 'none' })
        reject(err)
      }
    })
  })
}

export const get = (url, data) => request(url, 'GET', data)
export const post = (url, data, timeout) => request(url, 'POST', data, timeout)
export const put = (url, data) => request(url, 'PUT', data)
export const del = (url, data) => request(url, 'DELETE', data)
