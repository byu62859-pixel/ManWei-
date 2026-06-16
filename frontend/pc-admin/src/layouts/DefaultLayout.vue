<template>
  <el-container class="layout-container">
    <!-- 侧边栏 -->
    <el-aside class="aside" :class="{ 'aside--collapsed': isCollapsed }">
      <!-- Logo 区域 -->
      <div class="logo-area">
        <div class="logo-text">
          <span class="logo-title">漫味</span>
          <span class="logo-subtitle">ManWei</span>
        </div>
      </div>

      <!-- 导航菜单 -->
      <el-menu
        :default-active="$route.path"
        router
        class="aside-menu"
        :collapse="isCollapsed"
        :collapse-transition="false"
      >
        <el-menu-item index="/dashboard" class="menu-item--dashboard">
          <el-icon><DataAnalysis /></el-icon>
          <span>数据看板</span>
          <div class="menu-indicator"></div>
        </el-menu-item>
        <el-menu-item index="/anime" class="menu-item--anime">
          <el-icon><VideoCamera /></el-icon>
          <span>动漫管理</span>
          <div class="menu-indicator"></div>
        </el-menu-item>
        <el-menu-item index="/users" class="menu-item--users">
          <el-icon><User /></el-icon>
          <span>用户管理</span>
          <div class="menu-indicator"></div>
        </el-menu-item>
        <el-menu-item index="/emotion-tags" class="menu-item--tags">
          <el-icon><Collection /></el-icon>
          <span>情感标签</span>
          <div class="menu-indicator"></div>
        </el-menu-item>
        <el-menu-item index="/reviews" class="menu-item--reviews">
          <el-icon><Document /></el-icon>
          <span>观后感管理</span>
          <div class="menu-indicator"></div>
        </el-menu-item>
      </el-menu>

      <!-- 底部用户信息 -->
      <div class="sidebar-footer">
        <div class="user-avatar">
          <el-avatar :size="36">{{ authStore.nickName?.charAt(0) || 'M' }}</el-avatar>
        </div>
        <div class="user-info" v-if="!isCollapsed">
          <span class="user-name">{{ authStore.nickName }}</span>
          <span class="user-role">管理员</span>
        </div>
        <el-button
          type="danger"
          size="small"
          class="logout-btn"
          :class="{ 'logout-btn--icon': isCollapsed }"
          @click="handleLogout"
          :icon="isCollapsed ? 'Close' : ''"
        >
          {{ isCollapsed ? '' : '退出' }}
        </el-button>
      </div>
    </el-aside>

    <!-- 主内容区 -->
    <el-container class="main-container">
      <!-- 顶部栏 -->
      <el-header class="header">
        <!-- 面包屑/标题区 -->
        <div class="header-left">
          <div class="breadcrumb">
            <span class="breadcrumb-item">漫味</span>
            <span class="breadcrumb-sep">/</span>
            <span class="breadcrumb-item active">{{ pageTitle }}</span>
          </div>
        </div>

        <!-- 右侧操作区 -->
        <div class="header-right">
          <!-- 今日日期 -->
          <div class="today-date">
            <span class="date-day">{{ currentDay }}</span>
            <span class="date-label">{{ currentDateLabel }}</span>
          </div>
          <div class="header-divider"></div>
          <span class="nickname">{{ authStore.nickName }}</span>
          <div class="header-divider"></div>
          <el-button :icon="MagicStick" circle @click="aiDrawerVisible = true" title="AI 助手" />
        </div>
      </el-header>

      <!-- 页面内容 -->
      <el-main class="main">
        <router-view v-slot="{ Component }">
          <transition name="page-fade" mode="out-in">
            <component :is="Component" />
          </transition>
        </router-view>
      </el-main>
    </el-container>

    <AiChatDrawer v-model="aiDrawerVisible" />
  </el-container>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessageBox } from 'element-plus'
import { useAuthStore } from '@/stores/auth'
import { DataAnalysis, VideoCamera, User, Collection, Document, MagicStick } from '@element-plus/icons-vue'
import AiChatDrawer from '@/components/AiChatDrawer.vue'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const isCollapsed = ref(false)
const aiDrawerVisible = ref(false)

const pageTitleMap = {
  '/dashboard': '数据看板',
  '/anime': '动漫管理',
  '/users': '用户管理',
  '/emotion-tags': '情感标签管理',
  '/reviews': '观后感管理'
}
const pageTitle = computed(() => pageTitleMap[route.path] || '管理后台')

const currentDay = ref('')
const currentDateLabel = ref('')
const weekDayNames = ['周日', '周一', '周二', '周三', '周四', '周五', '周六']

onMounted(() => {
  const now = new Date()
  currentDay.value = now.getDate().toString().padStart(2, '0')
  currentDateLabel.value = `${(now.getMonth() + 1).toString().padStart(2, '0')}月${weekDayNames[now.getDay()]}`
})

const handleLogout = async () => {
  try {
    await ElMessageBox.confirm('确定要退出登录吗？', '确认退出', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
    authStore.logout()
    router.push('/login')
  } catch {
    // 用户取消
  }
}
</script>

<style scoped>
.layout-container {
  height: 100vh;
  display: flex;
}

/* ============================
   侧边栏
   ============================ */

.aside {
  width: 260px;
  background: var(--mw-sidebar-light);
  border-right: 1px solid var(--mw-sidebar-border);
  display: flex;
  flex-direction: column;
  transition: width var(--mw-dur-slow) var(--mw-ease-smooth);
  overflow: hidden;
  position: relative;
}

.aside--collapsed {
  width: 76px;
}

/* Logo 区域 */
.logo-area {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 0 24px;
  height: 68px;
  border-bottom: 1px solid var(--mw-sidebar-border);
  flex-shrink: 0;
}

.logo-text {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.logo-title {
  font-family: 'Sora', 'Noto Sans SC', sans-serif;
  font-size: 22px;
  font-weight: 700;
  color: var(--mw-sidebar-active);
  letter-spacing: 0.08em;
  line-height: 1.2;
  white-space: nowrap;
}

.logo-subtitle {
  font-size: 10px;
  color: var(--mw-sidebar-text);
  opacity: 0.6;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  font-weight: 500;
  white-space: nowrap;
}

/* 菜单 */
.aside-menu {
  flex: 1;
  border-right: none;
  background: transparent;
  padding: 16px 0;
  overflow-y: auto;
  overflow-x: hidden;
}

/* Custom scrollbar */
.aside-menu::-webkit-scrollbar {
  width: 4px;
}
.aside-menu::-webkit-scrollbar-track {
  background: transparent;
}
.aside-menu::-webkit-scrollbar-thumb {
  background: rgba(92, 74, 61, 0.15);
  border-radius: 2px;
}
.aside-menu::-webkit-scrollbar-thumb:hover {
  background: rgba(92, 74, 61, 0.25);
}

:deep(.el-menu-item) {
  height: 50px;
  line-height: 50px;
  color: var(--mw-sidebar-text);
  font-size: 14px;
  font-weight: 500;
  padding: 0 20px !important;
  margin: 4px 12px;
  border-radius: 12px;
  position: relative;
  transition: color var(--mw-dur-fast) var(--mw-ease),
              background var(--mw-dur-fast) var(--mw-ease),
              transform var(--mw-dur-fast) var(--mw-ease-spring),
              box-shadow var(--mw-dur-fast) var(--mw-ease) !important;
  display: flex;
  align-items: center;
  gap: 12px;
  overflow: hidden;
}

:deep(.el-menu-item .el-icon) {
  font-size: 18px;
  flex-shrink: 0;
  transition: transform var(--mw-dur-normal) var(--mw-ease-spring),
              color var(--mw-dur-fast) var(--mw-ease);
}

:deep(.el-menu-item span) {
  white-space: nowrap;
  opacity: 1;
  transition: opacity var(--mw-dur-normal) var(--mw-ease);
  font-weight: 500;
}

.aside--collapsed :deep(.el-menu-item span) {
  opacity: 0;
}

:deep(.el-menu-item.is-active) {
  background: linear-gradient(135deg, rgba(255, 138, 117, 0.15) 0%, rgba(255, 184, 122, 0.10) 100%) !important;
  color: var(--mw-sidebar-active) !important;
  font-weight: 600;
  box-shadow: 0 2px 12px rgba(255, 138, 117, 0.15);
}

:deep(.el-menu-item.is-active .el-icon) {
  transform: scale(1.12);
  color: var(--mw-sidebar-active);
}

/* 活跃项左侧强调线 */
.menu-indicator {
  position: absolute;
  left: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 3px;
  height: 0;
  background: linear-gradient(180deg, #FF8A75, #FFB87A);
  border-radius: 0 2px 2px 0;
  transition: height var(--mw-dur-normal) var(--mw-ease-spring),
              box-shadow var(--mw-dur-normal) var(--mw-ease);
}

:deep(.el-menu-item.is-active) .menu-indicator {
  height: 28px;
  box-shadow: 0 0 10px rgba(255, 138, 117, 0.45);
}

:deep(.el-menu-item:hover:not(.is-active)) {
  background: var(--mw-sidebar-hover);
  color: #8B6E5D;
}

/* 底部用户信息 */
.sidebar-footer {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 20px 20px 24px;
  border-top: 1px solid var(--mw-sidebar-border);
  animation: mw-sidebarSlide var(--mw-dur-slow) var(--mw-ease-out) 200ms forwards;
  opacity: 0;
}

.user-avatar {
  flex-shrink: 0;
}
.user-avatar :deep(.el-avatar) {
  box-shadow: 0 2px 10px rgba(92, 74, 61, 0.12);
  transition: transform var(--mw-dur-fast) var(--mw-ease-spring);
  background: var(--mw-coral-soft);
  color: var(--mw-coral);
  font-weight: 600;
}
.user-avatar:hover :deep(.el-avatar) {
  transform: scale(1.08);
}

.user-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.user-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--mw-sidebar-text);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.user-role {
  font-size: 11px;
  color: var(--mw-sidebar-text);
  opacity: 0.55;
  font-weight: 500;
}

.logout-btn {
  border-radius: 10px !important;
  font-size: 13px;
  flex-shrink: 0;
  background: rgba(255, 138, 117, 0.10) !important;
  border-color: transparent !important;
  color: var(--mw-coral) !important;
  transition: all var(--mw-dur-fast) var(--mw-ease-spring) !important;
}

.logout-btn:hover {
  background: rgba(255, 138, 117, 0.20) !important;
  color: var(--mw-coral-deep) !important;
  transform: scale(1.05);
  box-shadow: 0 4px 16px rgba(255, 138, 117, 0.25) !important;
}

.logout-btn--icon {
  padding: 6px 8px !important;
}

/* ============================
   主内容区
   ============================ */

.main-container {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

/* 顶部栏 */
:deep(.el-header),
.header {
  height: 68px !important;
  padding: 0 32px;
  display: flex;
  align-items: center;
  background: rgba(255, 255, 255, 0.92);
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  justify-content: space-between;
  box-shadow: 0 1px 0 var(--mw-border), 0 4px 16px rgba(45, 53, 97, 0.04);
  flex-shrink: 0;
  position: relative;
  z-index: 10;
}

/* Subtle peach bottom glow */
.header::after {
  content: '';
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 1px;
  background: linear-gradient(90deg, transparent 0%, rgba(255, 138, 117, 0.25) 50%, transparent 100%);
}

.header-left {
  display: flex;
  align-items: center;
}

.breadcrumb {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
}

.breadcrumb-item {
  color: var(--mw-text-muted);
  font-weight: 500;
  transition: color var(--mw-dur-fast) var(--mw-ease);
}
.breadcrumb-item:hover {
  color: var(--mw-text);
}
.breadcrumb-item.active {
  color: var(--mw-text);
  font-weight: 700;
}

.breadcrumb-sep {
  color: var(--mw-text-muted);
  font-weight: 300;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 20px;
}

.today-date {
  display: flex;
  align-items: baseline;
  gap: 8px;
  padding: 8px 16px;
  background: linear-gradient(135deg, rgba(255, 94, 98, 0.06) 0%, rgba(255, 138, 117, 0.04) 100%);
  border-radius: 12px;
  border: 1px solid rgba(255, 94, 98, 0.08);
}

.date-day {
  font-family: 'Sora', 'Noto Sans SC', sans-serif;
  font-size: 30px;
  font-weight: 700;
  color: var(--mw-coral);
  font-variant-numeric: tabular-nums;
  line-height: 1;
  letter-spacing: -0.02em;
}

.date-label {
  font-size: 12px;
  color: var(--mw-text-muted);
  font-weight: 500;
}

.header-divider {
  width: 1px;
  height: 28px;
  background: linear-gradient(180deg, transparent 0%, var(--mw-border) 50%, transparent 100%);
}

.nickname {
  font-size: 14px;
  font-weight: 600;
  color: var(--mw-text);
}

/* 页面内容 */
.main {
  flex: 1;
  background: linear-gradient(180deg, var(--mw-cream) 0%, var(--mw-cream-deep) 100%);
  padding: 28px 32px;
  overflow-y: auto;
  transition: background var(--mw-dur-normal) var(--mw-ease);
}

/* 页面切换动画 */
.page-fade-enter-active,
.page-fade-leave-active {
  transition: opacity var(--mw-dur-normal) var(--mw-ease),
              transform var(--mw-dur-normal) var(--mw-ease-smooth);
}
.page-fade-enter-from {
  opacity: 0;
  transform: translateY(12px);
}
.page-fade-leave-to {
  opacity: 0;
  transform: translateY(-8px) scale(0.99);
}
</style>