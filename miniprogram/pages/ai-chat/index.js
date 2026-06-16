const aiChat = require('../../utils/ai-chat')

Page({
  data: {
    inputText: '',
    messages: [],
    loading: false,
    scrollTop: 0,
    avatar: ''
  },

  onLoad() {
    const userInfo = wx.getStorageSync('userInfo')
    if (userInfo && userInfo.avatar) {
      this.setData({ avatar: userInfo.avatar })
    }
    this.loadHistory()
  },

  onShow() {
    // 不清除历史
  },

  onUnload() {
    // 不清除，保留到下次进入
  },

  loadHistory() {
    const history = wx.getStorageSync('ai_chat_history') || []
    if (history.length > 0) {
      this.setData({ messages: history })
      this.scrollToBottom()
    }
  },

  saveHistory() {
    const messages = this.data.messages
    const MAX = 20
    if (messages.length > MAX) {
      wx.setStorageSync('ai_chat_history', messages.slice(-MAX))
    } else {
      wx.setStorageSync('ai_chat_history', messages)
    }
  },

  onInput(e) {
    this.setData({ inputText: e.detail.value })
  },

  scrollToBottom() {
    setTimeout(() => {
      this.setData({ scrollTop: 999999 })
    }, 50)
  },

  async sendMessage() {
    const text = this.data.inputText.trim()
    if (!text || this.data.loading) return

    this.setData({ inputText: '' })
    const userMsg = { role: 'user', content: text }
    this.setData({ messages: [...this.data.messages, userMsg], loading: true })
    this.scrollToBottom()

    try {
      const result = await aiChat.chat(text)
      console.log('AI response:', JSON.stringify(result))
      const aiMsg = { role: 'assistant', content: result.answer || '抱歉，我现在有点问题，请稍后再试。' }
      const messages = [...this.data.messages, aiMsg]
      this.setData({ messages, loading: false })
      this.saveHistory()
      this.scrollToBottom()
    } catch (e) {
      const aiMsg = { role: 'assistant', content: '抱歉，AI 服务暂时不可用，请稍后再试。' }
      this.setData({ messages: [...this.data.messages, aiMsg], loading: false })
      this.saveHistory()
      this.scrollToBottom()
    }
  }
})
