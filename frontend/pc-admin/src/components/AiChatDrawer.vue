<template>
  <el-drawer v-model="visible" title="漫味小助手" direction="rtl" size="480px"
    :before-close="handleClose" class="ai-chat-drawer">
    <div class="chat-container">
      <div class="messages" ref="messagesRef">
        <div v-for="(msg, idx) in messages" :key="idx"
          :class="['message', msg.role === 'user' ? 'user-message' : 'assistant-message']">
          <div class="avatar">
            <el-icon v-if="msg.role === 'user'"><User /></el-icon>
            <el-icon v-else><MagicStick /></el-icon>
          </div>
          <div class="content">
            <div class="text" v-if="msg.role === 'user'">{{ msg.content }}</div>
            <div v-else>
              <div class="text" v-if="msg.displayType === 'text' || !msg.dataResults">{{ msg.content }}</div>
              <div v-else-if="msg.displayType === 'table'" class="data-table">
                <el-table :data="msg.dataResults" stripe size="small" max-height="300">
                  <el-table-column v-for="(col, ci) in getTableColumns(msg.dataResults)" :key="ci"
                    :prop="col" :label="col" min-width="100" />
                </el-table>
                <div class="text" v-if="msg.content">{{ msg.content }}</div>
              </div>
              <div v-else-if="msg.displayType === 'chart'" class="data-chart">
                <div ref="chartRef" style="width:100%;height:200px;"></div>
              </div>
              <div v-else-if="msg.displayType === 'card'" class="data-cards">
                <el-space direction="vertical" fill>
                  <el-card v-for="(item, ri) in msg.dataResults" :key="ri" shadow="hover">
                    <div v-for="(v, k) in item" :key="k" class="stat-item">
                      <span class="label">{{ k }}:</span>
                      <span class="value">{{ v }}</span>
                    </div>
                  </el-card>
                </el-space>
              </div>
              <div class="text" v-else>{{ msg.content }}</div>
            </div>
          </div>
        </div>
        <div v-if="loading" class="message assistant-message">
          <div class="avatar"><el-icon><MagicStick /></el-icon></div>
          <div class="content">
            <div class="loading-dots">
              <span></span><span></span><span></span>
            </div>
          </div>
        </div>
      </div>
      <div class="input-area">
        <el-input v-model="inputText" type="textarea" :rows="2" placeholder="问我任何关于用户、动漫、收藏的问题..."
          @keydown.enter.ctrl="sendMessage" resize="none" />
        <el-button type="primary" @click="sendMessage" :loading="loading" :disabled="!inputText.trim()">
          发送 (Ctrl+Enter)
        </el-button>
      </div>
    </div>
  </el-drawer>
</template>

<script setup>
import { ref, watch, nextTick } from 'vue'
import { User, MagicStick } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import * as echarts from 'echarts'
import request from '@/api'

const props = defineProps({ modelValue: Boolean })
const emit = defineEmits(['update:modelValue'])

const visible = ref(props.modelValue)
watch(() => props.modelValue, v => visible.value = v)
watch(visible, v => emit('update:modelValue', v))

const messages = ref([])
const inputText = ref('')
const loading = ref(false)
const messagesRef = ref(null)
const chartRef = ref(null)

const getTableColumns = (data) => {
  if (!data || data.length === 0) return []
  return Object.keys(data[0])
}

const scrollToBottom = () => {
  nextTick(() => {
    if (messagesRef.value)
      messagesRef.value.scrollTop = messagesRef.value.scrollHeight
  })
}

const sendMessage = async () => {
  const text = inputText.value.trim()
  if (!text || loading.value) return

  messages.value.push({ role: 'user', content: text })
  inputText.value = ''
  loading.value = true
  scrollToBottom()

  try {
    const json = await request.post('/api/AiAgent/chat', { message: text, history: buildHistory() })
    const data = json.data || json
    messages.value.push({
      role: 'assistant',
      content: data.answer || '',
      displayType: data.displayType || 'text',
      dataResults: data.dataResults || null
    })
    if (data.displayType === 'chart' && data.dataResults?.length > 0) {
      nextTick(() => renderChart(data.dataResults))
    }
  } catch (e) {
    ElMessage.error(e.message || 'AI 服务错误')
  } finally {
    loading.value = false
    scrollToBottom()
  }
}

const buildHistory = () => {
  const hist = messages.value.slice(-10)
  return hist.map(m => ({ role: m.role, content: m.content }))
}

const renderChart = (data) => {
  if (!chartRef.value || !data || data.length === 0) return
  const chart = echarts.init(chartRef.value)
  const keys = Object.keys(data[0])
  const xKey = keys[0]
  const yKey = keys[1]
  chart.setOption({
    tooltip: {},
    xAxis: { type: 'category', data: data.map(d => d[xKey]) },
    yAxis: { type: 'value' },
    series: [{ type: 'bar', data: data.map(d => d[yKey]), itemStyle: { color: '#409EFF' } }]
  })
}

const handleClose = () => {
  visible.value = false
}
</script>

<style scoped>
.ai-chat-drawer :deep(.el-drawer__header) {
  margin-bottom: 0;
  padding: 16px 20px;
  border-bottom: 1px solid #eee;
  font-weight: 600;
}
.chat-container {
  display: flex;
  flex-direction: column;
  height: 100%;
}
.messages {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
}
.message {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
}
.user-message { flex-direction: row-reverse; }
.avatar {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: #f0f2f5;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}
.assistant-message .avatar { background: #e6f7ff; color: #1890ff; }
.user-message .avatar { background: #f0f2f5; color: #666; }
.content { max-width: 75%; }
.text {
  background: #f5f5f5;
  padding: 10px 14px;
  border-radius: 12px;
  line-height: 1.5;
  word-break: break-word;
}
.user-message .text { background: #409EFF; color: #fff; }
.data-table { max-width: 100%; overflow-x: auto; margin-top: 8px; }
.data-cards .stat-item {
  display: flex;
  justify-content: space-between;
  padding: 4px 0;
}
.data-cards .label { color: #666; }
.data-cards .value { font-weight: 600; color: #333; }
.loading-dots {
  display: flex;
  gap: 4px;
  padding: 10px 14px;
  background: #f5f5f5;
  border-radius: 12px;
  width: fit-content;
}
.loading-dots span {
  width: 8px; height: 8px;
  background: #999;
  border-radius: 50%;
  animation: bounce 1.4s infinite ease-in-out;
}
.loading-dots span:nth-child(1) { animation-delay: -0.32s; }
.loading-dots span:nth-child(2) { animation-delay: -0.16s; }
@keyframes bounce {
  0%, 80%, 100% { transform: scale(0); }
  40% { transform: scale(1); }
}
.input-area {
  padding: 16px;
  border-top: 1px solid #eee;
  display: flex;
  gap: 10px;
  align-items: flex-end;
}
.input-area .el-textarea { flex: 1; }
</style>