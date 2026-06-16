<template>
  <div class="dashboard">
    <!-- 页面标题区 -->
    <div class="page-header">
      <div class="page-header__left">
        <h2 class="page-title">数据看板</h2>
        <p class="page-subtitle">实时掌握平台运营状况</p>
      </div>
      <div class="page-header__right">
        <span class="update-time">最后更新: {{ currentTime }}</span>
      </div>
    </div>

    <!-- 第一行：4个统计卡片 -->
    <el-row :gutter="20" class="stat-row">
      <el-col :span="6" v-for="(stat, idx) in statCards" :key="stat.key">
        <el-card class="stat-card" :class="'stat-card--' + stat.key" :style="{ animationDelay: (idx * 80) + 'ms' }">
          <!-- 装饰圆形 -->
          <div class="stat-decor"></div>
          <!-- 图标区 -->
          <div class="stat-icon">
            <component :is="stat.icon" />
          </div>
          <!-- 内容区 -->
          <div class="stat-body">
            <div class="stat-value" :style="{ color: stat.color }">{{ stats[stat.key] || 0 }}</div>
            <div class="stat-label">{{ stat.label }}</div>
          </div>
          <!-- 趋势指示 -->
          <div class="stat-trend" v-if="stat.trend">
            <span class="trend-up">↑ {{ stat.trend }}%</span>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 第二行：用户增长趋势 + 标签TOP10 -->
    <el-row :gutter="20" class="chart-row">
      <el-col :span="16">
        <el-card class="chart-card chart-card--wide">
          <template #header>
            <div class="chart-header">
              <span class="chart-title">
                <span class="chart-title__icon">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/>
                  </svg>
                </span>
                用户增长趋势
              </span>
              <span class="chart-subtitle">近30日</span>
            </div>
          </template>
          <div v-if="userGrowthLoading" class="chart-loading">
            <el-skeleton animated />
          </div>
          <v-chart v-else :option="userGrowthOption" style="height: 280px" />
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card class="chart-card">
          <template #header>
            <div class="chart-header">
              <span class="chart-title">
                <span class="chart-title__icon">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"/>
                    <line x1="7" y1="7" x2="7.01" y2="7"/>
                  </svg>
                </span>
                标签 TOP10
              </span>
              <span class="chart-subtitle">使用频率</span>
            </div>
          </template>
          <div v-if="tagRankLoading" class="chart-loading">
            <el-skeleton animated />
          </div>
          <v-chart v-else :option="tagRankOption" style="height: 280px" />
        </el-card>
      </el-col>
    </el-row>

    <!-- 第三行：收藏排行榜 + 今日概览 -->
    <el-row :gutter="20" class="chart-row">
      <el-col :span="16">
        <el-card class="chart-card chart-card--wide">
          <template #header>
            <div class="chart-header">
              <span class="chart-title">
                <span class="chart-title__icon">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/>
                  </svg>
                </span>
                动漫收藏排行榜
              </span>
              <span class="chart-subtitle">TOP 10</span>
            </div>
          </template>
          <div v-if="animeRankLoading" class="chart-loading">
            <el-skeleton animated />
          </div>
          <v-chart v-else :option="animeRankOption" style="height: 280px" />
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card class="chart-card">
          <template #header>
            <div class="chart-header">
              <span class="chart-title">
                <span class="chart-title__icon">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="12" cy="12" r="10"/>
                    <polyline points="12 6 12 12 16 14"/>
                  </svg>
                </span>
                今日概览
              </span>
              <span class="chart-subtitle">{{ todayDate }}</span>
            </div>
          </template>
          <div v-if="todayLoading" class="chart-loading">
            <el-skeleton animated />
          </div>
          <div v-else class="today-list">
            <div
              v-for="item in todayItems"
              :key="item.key"
              class="today-item"
              :style="{ '--item-color': item.color }"
            >
              <div class="today-item__left">
                <div class="today-dot"></div>
                <span class="today-label">{{ item.label }}</span>
              </div>
              <span class="today-value" :style="{ color: item.color }">{{ item.value }}</span>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, computed, h } from 'vue'
import { ElMessage } from 'element-plus'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { LineChart, BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import { getStats, getTodayOverview, getUserGrowth, getAnimeRank, getTagRank } from '@/api/dashboard'

use([CanvasRenderer, LineChart, BarChart, GridComponent, TooltipComponent, LegendComponent])

// 当前时间
const currentTime = ref('')
const todayDate = computed(() => {
  const now = new Date()
  return `${now.getMonth() + 1}月${now.getDate()}日`
})

// 统计数据
const loading = ref(true)
const stats = reactive({ totalUsers: 0, totalAnime: 0, totalFavorites: 0, totalEmotionTags: 0 })

// 统计卡片配置
const statCards = [
  { key: 'totalUsers',      label: '用户总数',      icon: UserIcon,    color: '#FF5E62', trend: 12 },
  { key: 'totalAnime',      label: '动漫总数',      icon: AnimeIcon,   color: '#9B6EF3', trend: 8 },
  { key: 'totalFavorites',   label: '收藏总数',      icon: StarIcon,    color: '#4ECCA3', trend: 23 },
  { key: 'totalEmotionTags', label: '情感标签',     icon: TagIcon,     color: '#FFB830', trend: 5 }
]

// 今日概览
const todayLoading = ref(true)
const todayOverview = reactive({ newUsers: 0, newFavorites: 0, newTags: 0, newAnime: 0 })

const todayItems = computed(() => [
  { key: 'newUsers',     label: '新增用户',   value: todayOverview.newUsers,     color: '#FF5E62' },
  { key: 'newFavorites', label: '新增收藏',   value: todayOverview.newFavorites, color: '#4ECCA3' },
  { key: 'newTags',      label: '新增标签',   value: todayOverview.newTags,      color: '#9B6EF3' },
  { key: 'newAnime',     label: '新增动漫',   value: todayOverview.newAnime,     color: '#FFB830' }
])

// 用户增长趋势
const userGrowthLoading = ref(true)
const userGrowthData = ref([])

const userGrowthOption = computed(() => ({
  tooltip: {
    trigger: 'axis',
    backgroundColor: 'rgba(255,255,255,0.95)',
    borderColor: '#EDE9E3',
    borderWidth: 1,
    textStyle: { color: '#2D3561', fontSize: 12 },
    padding: [8, 12]
  },
  grid: { left: '3%', right: '4%', bottom: '3%', top: '12%', containLabel: true },
  xAxis: {
    type: 'category',
    data: userGrowthData.value.map(d => d.date),
    axisLabel: { color: '#A8AEC0', fontSize: 11 },
    axisLine: { lineStyle: { color: '#EDE9E3' } },
    axisTick: { show: false }
  },
  yAxis: {
    type: 'value',
    axisLabel: { color: '#A8AEC0', fontSize: 11 },
    splitLine: { lineStyle: { color: '#F3EEE8', type: 'dashed' } },
    axisLine: { show: false },
    axisTick: { show: false }
  },
  series: [{
    name: '新增用户',
    type: 'line',
    smooth: 0.4,
    data: userGrowthData.value.map(d => d.userCount),
    areaStyle: {
      color: {
        type: 'linear',
        x: 0, y: 0, x2: 0, y2: 1,
        colorStops: [
          { offset: 0, color: 'rgba(255, 94, 98, 0.28)' },
          { offset: 1, color: 'rgba(255, 94, 98, 0)' }
        ]
      }
    },
    lineStyle: { color: '#FF5E62', width: 2.5 },
    itemStyle: { color: '#FF5E62' },
    symbol: 'circle',
    symbolSize: 6,
    showSymbol: false,
    emphasis: { showSymbol: true }
  }]
}))

// 标签TOP10横向柱状图
const tagRankLoading = ref(true)
const tagRankData = ref([])

const tagColors = ['#FF5E62','#FF8A75','#FFB830','#4ECCA3','#4AACFC','#9B6EF3','#FF6B8A','#FF8A75','#FFB830','#4ECCA3']

const tagRankOption = computed(() => ({
  tooltip: {
    trigger: 'axis',
    backgroundColor: 'rgba(255,255,255,0.95)',
    borderColor: '#EDE9E3',
    textStyle: { color: '#2D3561', fontSize: 12 }
  },
  grid: { left: '3%', right: '8%', bottom: '3%', top: '3%', containLabel: true },
  xAxis: {
    type: 'value',
    axisLabel: { color: '#A8AEC0', fontSize: 11 },
    splitLine: { lineStyle: { color: '#F3EEE8', type: 'dashed' } },
    axisLine: { show: false },
    axisTick: { show: false }
  },
  yAxis: {
    type: 'category',
    data: tagRankData.value.map(d => d.tagName).reverse(),
    axisLabel: { color: '#6B7494', fontSize: 12, fontWeight: 500 },
    axisLine: { show: false },
    axisTick: { show: false }
  },
  series: [{
    name: '使用次数',
    type: 'bar',
    data: tagRankData.value.map(d => d.usageCount).reverse(),
    barWidth: '55%',
    itemStyle: {
      color: (params) => tagColors[params.dataIndex % tagColors.length],
      borderRadius: [0, 6, 6, 0]
    },
    emphasis: { itemStyle: { shadowBlur: 10, shadowColor: 'rgba(0,0,0,0.15)' } }
  }]
}))

// 动漫收藏排行榜
const animeRankLoading = ref(true)
const animeRankData = ref([])

const animeRankOption = computed(() => ({
  tooltip: {
    trigger: 'axis',
    backgroundColor: 'rgba(255,255,255,0.95)',
    borderColor: '#EDE9E3',
    textStyle: { color: '#2D3561', fontSize: 12 }
  },
  grid: { left: '3%', right: '4%', bottom: '12%', top: '12%', containLabel: true },
  xAxis: {
    type: 'category',
    data: animeRankData.value.map(d => d.animeName),
    axisLabel: { color: '#A8AEC0', fontSize: 11, rotate: 25, interval: 0 },
    axisLine: { lineStyle: { color: '#EDE9E3' } },
    axisTick: { show: false }
  },
  yAxis: {
    type: 'value',
    axisLabel: { color: '#A8AEC0', fontSize: 11 },
    splitLine: { lineStyle: { color: '#F3EEE8', type: 'dashed' } },
    axisLine: { show: false },
    axisTick: { show: false }
  },
  series: [{
    name: '收藏数',
    type: 'bar',
    data: animeRankData.value.map(d => d.favoriteCount),
    barWidth: '45%',
    itemStyle: {
      color: {
        type: 'linear',
        x: 0, y: 0, x2: 0, y2: 1,
        colorStops: [
          { offset: 0, color: '#A07EF6' },
          { offset: 1, color: '#74C0FC' }
        ]
      },
      borderRadius: [6, 6, 0, 0]
    },
    emphasis: { itemStyle: { shadowBlur: 10, shadowColor: 'rgba(160,126,246,0.3)' } }
  }]
}))

// 更新当前时间
const updateTime = () => {
  const now = new Date()
  currentTime.value = `${now.getHours().toString().padStart(2,'0')}:${now.getMinutes().toString().padStart(2,'0')}:${now.getSeconds().toString().padStart(2,'0')}`
}

onMounted(async () => {
  updateTime()
  setInterval(updateTime, 1000)

  try {
    const [statsData, today, growth, animeRank, tagRank] = await Promise.all([
      getStats(),
      getTodayOverview(),
      getUserGrowth(30),
      getAnimeRank(10),
      getTagRank()
    ])
    Object.assign(stats, statsData)
    Object.assign(todayOverview, today)
    userGrowthData.value = growth.map(d => ({ date: d.date?.split('T')[0] || d.date, userCount: d.userCount }))
    animeRankData.value = animeRank
    tagRankData.value = tagRank
  } catch (error) {
    ElMessage.error(error.message || '加载数据失败')
  } finally {
    loading.value = false
    todayLoading.value = false
    userGrowthLoading.value = false
    animeRankLoading.value = false
    tagRankLoading.value = false
  }
})

// SVG 图标组件
function UserIcon() {
  return h('svg', { width: 28, height: 28, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', 'stroke-width': '2' }, [
    h('path', { d: 'M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2' }),
    h('circle', { cx: '12', cy: '7', r: '4' })
  ])
}

function AnimeIcon() {
  return h('svg', { width: 28, height: 28, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', 'stroke-width': '2' }, [
    h('rect', { x: '2', y: '2', width: '20', height: '20', rx: '3' }),
    h('path', { d: 'M7 12l4 4 6-6' })
  ])
}

function StarIcon() {
  return h('svg', { width: 28, height: 28, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', 'stroke-width': '2' }, [
    h('polygon', { points: '12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2' })
  ])
}

function TagIcon() {
  return h('svg', { width: 28, height: 28, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', 'stroke-width': '2' }, [
    h('path', { d: 'M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z' }),
    h('line', { x1: '7', y1: '7', x2: '7.01', y2: '7' })
  ])
}
</script>

<style scoped>
.dashboard {
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) forwards;
}

/* 页面标题区 */
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 28px;
}

.page-header__left {
  opacity: 0;
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) forwards;
}

.page-title {
  font-family: 'Sora', 'Noto Sans SC', sans-serif;
  font-size: 28px;
  font-weight: 700;
  color: var(--mw-text);
  margin-bottom: 6px;
  letter-spacing: -0.03em;
}

.page-subtitle {
  font-size: 14px;
  color: var(--mw-text-muted);
}

.page-header__right {
  opacity: 0;
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) 100ms forwards;
}

.update-time {
  font-size: 12px;
  color: var(--mw-text-muted);
  font-variant-numeric: tabular-nums;
}

/* 统计卡片行 */
.stat-row {
  margin-bottom: 20px;
}

/* 统计卡片 */
.stat-card {
  position: relative;
  overflow: hidden;
  border-radius: 16px;
  border: none;
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) forwards;
  opacity: 0;
}

.stat-card::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 3px;
  background: v-bind('statCards[0]?.color');
}

.stat-decor {
  position: absolute;
  top: -30px;
  right: -30px;
  width: 100px;
  height: 100px;
  border-radius: 50%;
  opacity: 0.06;
  background: v-bind('statCards[0]?.color');
}

.stat-card :deep(.el-card__body) {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 20px;
}

.stat-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 56px;
  height: 56px;
  border-radius: 14px;
  font-size: 26px;
  flex-shrink: 0;
  transition: transform var(--mw-dur-normal) var(--mw-ease-bounce);
  background: v-bind('statCards[0] ? statCards[0].color + "18" : "#fff"');
  color: v-bind('statCards[0]?.color');
}

.stat-card:hover .stat-icon {
  transform: scale(1.12) rotate(5deg);
}

.stat-body {
  flex: 1;
  min-width: 0;
}

.stat-value {
  font-size: 30px;
  font-weight: 700;
  color: var(--mw-text);
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
  animation: mw-countUp var(--mw-dur-slow) var(--mw-ease-out) forwards;
  animation-delay: 200ms;
  opacity: 0;
}

.stat-label {
  margin-top: 4px;
  font-size: 13px;
  color: var(--mw-text-muted);
}

.stat-trend {
  position: absolute;
  top: 14px;
  right: 16px;
}

.trend-up {
  font-size: 11px;
  color: var(--mw-mint);
  font-weight: 600;
}

/* 图表行 */
.chart-row {
  margin-bottom: 20px;
}

.chart-card {
  border-radius: 16px;
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) 200ms forwards;
  opacity: 0;
}

.chart-card--wide {
  animation-delay: 100ms;
}

.chart-card :deep(.el-card__header) {
  padding: 14px 20px;
  border-bottom: none;
}

.chart-card :deep(.el-card__body) {
  padding: 16px 20px 20px;
}

.chart-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.chart-title {
  font-size: 15px;
  font-weight: 700;
  color: var(--mw-text);
  display: flex;
  align-items: center;
  gap: 8px;
}

.chart-title__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  background: var(--mw-coral-soft);
  border-radius: 8px;
  color: var(--mw-coral);
}

.chart-subtitle {
  font-size: 12px;
  color: var(--mw-text-muted);
}

.chart-loading {
  height: 280px;
  padding: 8px;
}

/* 今日概览 */
.today-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 8px 0;
}

.today-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 18px;
  background: var(--mw-surface);
  border-radius: 12px;
  border-left: 4px solid var(--item-color, var(--mw-coral));
  transition: background var(--mw-dur-fast),
              transform var(--mw-dur-fast) var(--mw-ease-spring),
              box-shadow var(--mw-dur-fast) var(--mw-ease);
}

.today-item:hover {
  background: var(--mw-cream);
  transform: translateX(6px);
  box-shadow: var(--mw-shadow-sm);
}

.today-item__left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.today-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--item-color, var(--mw-coral));
}

.today-label {
  font-size: 14px;
  color: var(--mw-text-soft);
}

.today-value {
  font-size: 24px;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

/* 响应式 */
@media (max-width: 1200px) {
  .stat-row .el-col { margin-bottom: 16px; }
}
</style>