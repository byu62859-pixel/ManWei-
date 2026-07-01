<template>
  <div class="user-manage-page">
    <!-- 页面标题 -->
    <div class="page-header">
      <div class="page-header__left">
        <h2 class="page-title">用户管理</h2>
        <p class="page-subtitle">管理平台注册用户</p>
      </div>
      <div class="page-header__right">
        <div class="user-stat">
          <span class="user-stat__num">{{ pagination.total }}</span>
          <span class="user-stat__label">位用户</span>
        </div>
      </div>
    </div>

    <!-- 搜索栏 -->
    <div class="toolbar">
      <el-input
        v-model="keyword"
        placeholder="搜索 OpenId / 昵称..."
        prefix-icon="Search"
        clearable
        @keyup.enter="handleSearch"
        @clear="handleSearch"
        class="search-input"
      />
      <el-button type="primary" class="search-btn" @click="handleSearch">
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
          <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
        </svg>
        查询
      </el-button>
    </div>

    <!-- 用户表格 -->
    <el-card class="table-card">
      <el-table :data="users" v-loading="loading" stripe>
        <el-table-column prop="id" label="ID" width="80" />
        <el-table-column prop="avatar" label="头像" width="80">
          <template #default="{ row }">
            <el-avatar
              v-if="row.avatar"
              :src="getAvatarUrl(row.avatar)"
              :size="40"
              class="user-avatar"
            />
            <el-avatar v-else :size="40" class="user-avatar user-avatar--default">
              {{ (row.nickName || row.openId || '?').charAt(0).toUpperCase() }}
            </el-avatar>
          </template>
        </el-table-column>
        <el-table-column prop="openId" label="OpenId" min-width="200" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="open-id">{{ row.openId }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="nickName" label="昵称" min-width="120">
          <template #default="{ row }">
            <span class="nickname-text" :class="{ 'nickname-text--empty': !row.nickName }">
              {{ row.nickName || '-' }}
            </span>
          </template>
        </el-table-column>
        <el-table-column prop="role" label="角色" width="100">
          <template #default="{ row }">
            <span class="role-badge" :class="row.role === 'Admin' ? 'role-badge--admin' : 'role-badge--user'">
              {{ row.role === 'Admin' ? '管理员' : '用户' }}
            </span>
          </template>
        </el-table-column>
        <el-table-column prop="isEnabled" label="状态" width="100">
          <template #default="{ row }">
            <el-switch
              v-model="row.isEnabled"
              :disabled="row.id === currentUserId"
              class="status-switch"
              @change="handleSwitch(row)"
            />
          </template>
        </el-table-column>
        <el-table-column prop="createTime" label="注册时间" width="180">
          <template #default="{ row }">
            <span class="time-text">{{ formatDate(row.createTime) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="80" fixed="right">
          <template #default="{ row }">
            <div class="op-btn-group">
              <el-button
                size="small"
                class="op-btn op-btn--delete"
                :disabled="row.id === currentUserId"
                @click="handleDelete(row)"
              >
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
                </svg>
              </el-button>
            </div>
          </template>
        </el-table-column>
      </el-table>

      <!-- 分页 -->
      <div class="pagination-wrapper">
        <el-pagination
          v-model:current-page="pagination.page"
          v-model:page-size="pagination.pageSize"
          :total="pagination.total"
          :page-sizes="[10, 20, 50]"
          layout="total, sizes, prev, pager, next, jumper"
          @size-change="handleSizeChange"
          @current-change="handleCurrentChange"
        />
      </div>
    </el-card>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getUserList, updateUserStatus, deleteUser } from '@/api/user'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
const currentUserId = authStore.userId
const baseURL = ''
const getAvatarUrl = (avatar) => avatar ? (avatar.startsWith('http') ? avatar : baseURL + avatar) : ''

const loading = ref(true)
const users = ref([])
const keyword = ref('')

const pagination = reactive({
  page: 1,
  pageSize: 20,
  total: 0
})

const formatDate = (dateStr) => {
  if (!dateStr) return '-'
  const d = new Date(dateStr + (dateStr.endsWith('Z') ? '' : 'Z'))
  return d.toLocaleString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Shanghai' })
}

const loadUsers = async () => {
  loading.value = true
  try {
    const result = await getUserList({
      page: pagination.page,
      pageSize: pagination.pageSize,
      keyword: keyword.value || undefined
    })
    users.value = result.items
    pagination.total = result.totalCount
  } catch (error) {
    ElMessage.error(error.message || '加载用户列表失败')
  } finally {
    loading.value = false
  }
}

const handleSearch = () => {
  pagination.page = 1
  loadUsers()
}

const handleSizeChange = (size) => {
  pagination.pageSize = size
  pagination.page = 1
  loadUsers()
}

const handleCurrentChange = (page) => {
  pagination.page = page
  loadUsers()
}

const handleSwitch = async (row) => {
  const originalState = !row.isEnabled
  try {
    await updateUserStatus(row.id, { isEnabled: row.isEnabled })
    ElMessage.success(row.isEnabled ? '已启用' : '已禁用')
  } catch (error) {
    row.isEnabled = originalState
    ElMessage.error(error.message || '操作失败')
  }
}

const handleDelete = async (row) => {
  try {
    await ElMessageBox.confirm(
      `确定删除用户"${row.nickName || row.openId}"吗？此操作不可恢复。`,
      '确认删除',
      { type: 'warning' }
    )
    await deleteUser(row.id)
    ElMessage.success('删除成功')
    await loadUsers()
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error.message || '删除失败')
    }
  }
}

onMounted(() => {
  loadUsers()
})
</script>

<style scoped>
.user-manage-page {
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) forwards;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 24px;
}
.page-header__left {
  opacity: 0;
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) forwards;
}
.page-title {
  font-size: 26px;
  font-weight: 700;
  color: var(--mw-text);
  margin-bottom: 6px;
}
.page-subtitle {
  font-size: 14px;
  color: var(--mw-text-muted);
}
.page-header__right {
  opacity: 0;
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) 100ms forwards;
}
.user-stat {
  display: flex;
  align-items: baseline;
  gap: 4px;
  background: var(--mw-lavender-soft);
  padding: 8px 16px;
  border-radius: 10px;
}
.user-stat__num {
  font-size: 22px;
  font-weight: 700;
  color: var(--mw-lavender);
  font-variant-numeric: tabular-nums;
}
.user-stat__label {
  font-size: 12px;
  color: var(--mw-lavender);
  opacity: 0.7;
}

.toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}
.search-input {
  width: 320px;
}
.search-btn {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  border-radius: 10px !important;
}

.table-card {
  border-radius: 16px;
}

:deep(.el-table) {
  font-size: 14px;
}
:deep(.el-table th) {
  font-size: 12px !important;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.user-avatar {
  box-shadow: var(--mw-shadow-sm);
  transition: transform var(--mw-dur-fast) var(--mw-ease-bounce);
}
.user-avatar:hover {
  transform: scale(1.1);
}
.user-avatar--default {
  background: linear-gradient(135deg, var(--mw-coral) 0%, var(--mw-peach) 100%);
  color: #fff;
  font-weight: 600;
}

.open-id {
  font-family: 'Courier New', monospace;
  font-size: 11px;
  color: var(--mw-text-muted);
}

.nickname-text {
  font-weight: 500;
  color: var(--mw-text);
}
.nickname-text--empty {
  color: var(--mw-text-muted);
}

.role-badge {
  display: inline-flex;
  align-items: center;
  padding: 3px 10px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 600;
}
.role-badge--admin {
  background: var(--mw-coral-soft);
  color: #d94040;
}
.role-badge--user {
  background: var(--mw-sky-soft);
  color: #3a7fc0;
}

.status-switch {
  --el-switch-off-color: var(--mw-border);
  --el-switch-on-color: var(--mw-mint);
}

.time-text {
  color: var(--mw-text-muted);
  font-size: 12px;
}

.op-btn-group {
  display: flex;
  align-items: center;
  gap: 6px;
}

.op-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  padding: 0 !important;
  border-radius: 6px !important;
  transition: all var(--mw-dur-fast) !important;
}

.op-btn:not(:disabled):hover {
  transform: translateY(-1px);
}

/* 删除按钮 - 红色系 */
.op-btn--delete {
  background: rgba(255, 94, 98, 0.12) !important;
  border: 1px solid rgba(255, 94, 98, 0.25) !important;
  color: #FF5E62 !important;
}
.op-btn--delete:not(:disabled):hover {
  background: rgba(255, 94, 98, 0.22) !important;
  border-color: rgba(255, 94, 98, 0.40) !important;
  box-shadow: 0 2px 8px rgba(255, 94, 98, 0.25) !important;
}
.op-btn--delete:disabled {
  opacity: 0.4 !important;
}

.pagination-wrapper {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
}

/* 分页按钮样式 */
:deep(.el-pagination) {
  --el-pagination-button-bg-color: rgba(255, 94, 98, 0.08);
  --el-pagination-button-color: var(--mw-text);
  --el-pagination-hover-color: var(--mw-coral);
}

:deep(.el-pagination .el-pager li) {
  background: rgba(255, 94, 98, 0.06) !important;
  color: var(--mw-text) !important;
  border-radius: 6px;
  margin: 0 2px;
  min-width: 32px;
  height: 32px;
  line-height: 32px;
  font-weight: 500;
}

:deep(.el-pagination .el-pager li:hover) {
  color: var(--mw-coral) !important;
}

:deep(.el-pagination .el-pager li.is-active) {
  background: var(--mw-coral) !important;
  color: #fff !important;
}

:deep(.el-pagination .el-pagination__jump) {
  color: var(--mw-text);
}
</style>