const { post } = require('./api')

const chat = (message, history = []) => {
  return post('/api/WxAiAgent/chat', { message, history }, 180000)
}

module.exports = { chat }
