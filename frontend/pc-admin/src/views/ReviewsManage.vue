<template>
  <div class="reviews-manage-page">
    <!-- 页面标题 -->
    <div class="page-header">
      <div class="page-header__left">
        <h2 class="page-title">观后感管理</h2>
        <p class="page-subtitle">审核与管理平台全部观后感内容</p>
      </div>
      <div class="page-header__right">
        <div class="review-stat">
          <span class="review-stat__num">{{ pagination.total }}</span>
          <span class="review-stat__label">条观后感</span>
        </div>
      </div>
    </div>

    <!-- 搜索栏 -->
    <div class="toolbar">
      <el-input
        v-model="keyword"
        placeholder="搜索观后感内容..."
        prefix-icon="Search"
        clearable
        class="search-input"
        @keyup.enter="handleSearch"
        @clear="handleSearch"
      />
    </div>

    <!-- 观后感表格 -->
    <el-card class="table-card">
      <el-table :data="reviewList" v-loading="loading" stripe>
        <el-table-column label="动漫" width="220">
          <template #default="{ row }">
            <div class="anime-cell">
              <el-image
                v-if="row.animeCover"
                :src="row.animeCover"
                fit="cover"
                class="anime-cover"
              />
              <div v-else class="anime-cover-placeholder">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                  <rect x="2" y="2" width="20" height="20" rx="3"/>
                  <circle cx="8" cy="8" r="1.5"/>
                  <path d="M21 15l-5-5L5 20"/>
                </svg>
              </div>
              <span class="anime-name" :title="row.animeName">{{ row.animeName }}</span>
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="nickName" label="用户" width="120">
          <template #default="{ row }">
            <div class="user-cell">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/>
                <circle cx="12" cy="7" r="4"/>
              </svg>
              <span>{{ row.nickName }}</span>
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="contentSummary" label="内容摘要" min-width="200">
          <template #default="{ row }">
            <el-tooltip :content="row.contentSummary" placement="top" :open-delay="300">
              <span class="content-summary">{{ row.contentSummary }}</span>
            </el-tooltip>
          </template>
        </el-table-column>
        <el-table-column prop="updatedAt" label="发布时间" width="160">
          <template #default="{ row }">
            <span class="time-text">{{ formatDate(row.updatedAt) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="120" fixed="right">
          <template #default="{ row }">
            <div class="op-btn-group">
              <el-button
                size="small"
                class="op-btn op-btn--view"
                @click="handleView(row)"
              >
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
                  <circle cx="12" cy="12" r="3"/>
                </svg>
              </el-button>
              <el-button
                size="small"
                class="op-btn op-btn--delete"
                @click="handleDelete(row)"
              >
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <polyline points="3 6 5 6 21 6"/>
                  <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
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

    <!-- 查看观后感详情弹窗 -->
    <el-dialog
      v-model="viewDialogVisible"
      title="观后感详情"
      width="600px"
      :destroy-on-close="true"
      append-to-body="true"
      class="review-detail-dialog"
    >
      <div v-if="viewDialogLoading" class="view-dialog-loading">
        <el-icon class="is-loading"><Loading /></el-icon>
      </div>
      <div v-else-if="currentReview" class="review-detail">
        <div class="review-detail__header">
          <el-image
            v-if="currentReview.animeCover"
            :src="currentReview.animeCover"
            fit="cover"
            class="review-detail__cover"
          />
          <div class="review-detail__info">
            <h3 class="review-detail__anime-name">{{ currentReview.animeName }}</h3>
            <p class="review-detail__user">用户：{{ currentReview.nickName }}</p>
            <p class="review-detail__time">{{ formatDate(currentReview.updatedAt) }}</p>
          </div>
        </div>
        <el-divider />
        <div class="review-detail__content">
          <p>{{ currentReview.content }}</p>
        </div>
      </div>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getAdminReviewList, getReviewDetail, deleteReview } from '@/api/review'

const loading = ref(true)
const reviewList = ref([])
const keyword = ref('')
const viewDialogVisible = ref(false)
const viewDialogLoading = ref(false)
const currentReview = ref(null)

const pagination = reactive({
  page: 1,
  pageSize: 20,
  total: 0
})

const formatDate = (dateStr) => {
  if (!dateStr) return '-'
  const d = new Date(dateStr + (dateStr.endsWith('Z') ? '' : 'Z'))
  return d.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'Asia/Shanghai'
  })
}

const loadReviews = async () => {
  loading.value = true
  try {
    const result = await getAdminReviewList({
      page: pagination.page,
      pageSize: pagination.pageSize,
      keyword: keyword.value || undefined
    })
    reviewList.value = result.items
    pagination.total = result.totalCount
  } catch (error) {
    ElMessage.error(error.message || '加载观后感列表失败')
  } finally {
    loading.value = false
  }
}

const handleSearch = () => {
  pagination.page = 1
  loadReviews()
}

const handleSizeChange = (size) => {
  pagination.pageSize = size
  pagination.page = 1
  loadReviews()
}

const handleCurrentChange = (page) => {
  pagination.page = page
  loadReviews()
}

const handleView = async (row) => {
  viewDialogVisible.value = true
  viewDialogLoading.value = true
  currentReview.value = null
  try {
    const result = await getReviewDetail(row.favoriteId)
    currentReview.value = result
  } catch (error) {
    ElMessage.error(error.message || '获取观后感详情失败')
    viewDialogVisible.value = false
  } finally {
    viewDialogLoading.value = false
  }
}

const handleDelete = async (row) => {
  try {
    await ElMessageBox.confirm(
      `确定删除"${row.animeName}"的观后感吗？该操作不可恢复。`,
      '确认删除',
      { type: 'warning' }
    )
    await deleteReview(row.favoriteId)
    ElMessage.success('删除成功')
    await loadReviews()
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error.message || '删除失败')
    }
  }
}

onMounted(() => {
  loadReviews()
})
</script>

<style scoped>
.reviews-manage-page {
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

.review-stat {
  display: flex;
  align-items: baseline;
  gap: 6px;
  padding: 8px 16px;
  background: var(--mw-coral-soft);
  border-radius: 10px;
}
.review-stat__num {
  font-size: 24px;
  font-weight: 700;
  color: var(--mw-coral);
  font-variant-numeric: tabular-nums;
}
.review-stat__label {
  font-size: 13px;
  color: var(--mw-coral);
  opacity: 0.7;
}

.toolbar {
  margin-bottom: 16px;
}
.search-input {
  width: 360px;
}

.table-card {
  border-radius: 16px;
}

:deep(.el-table) {
  font-size: 14px;
}

:deep(.el-table .cell) {
  overflow: hidden;
  text-overflow: ellipsis;
}

.anime-cell {
  display: flex;
  align-items: center;
  gap: 10px;
}
.anime-cover {
  width: 40px;
  height: 56px;
  border-radius: 6px;
  flex-shrink: 0;
  object-fit: cover;
}
.anime-cover-placeholder {
  width: 40px;
  height: 56px;
  border-radius: 6px;
  background: var(--mw-cream-deep);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--mw-text-muted);
  flex-shrink: 0;
}
.anime-name {
  font-weight: 600;
  color: var(--mw-text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-cell {
  display: flex;
  align-items: center;
  gap: 6px;
  color: var(--mw-text-soft);
  font-size: 13px;
}

.content-summary {
  color: var(--mw-text-soft);
  font-size: 13px;
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  cursor: default;
}

:deep(.el-tooltip__trigger) {
  width: 100%;
}

:deep(.el-tooltip__popper) {
  max-width: 400px;
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

/* 查看按钮 - 蓝色系 */
.op-btn--view {
  background: rgba(64, 158, 255, 0.12) !important;
  border: 1px solid rgba(64, 158, 255, 0.25) !important;
  color: #409EFF !important;
}
.op-btn--view:not(:disabled):hover {
  background: rgba(64, 158, 255, 0.22) !important;
  border-color: rgba(64, 158, 255, 0.40) !important;
  box-shadow: 0 2px 8px rgba(64, 158, 255, 0.25) !important;
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

/* 查看弹窗 */
.view-dialog-loading {
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 40px;
  font-size: 24px;
  color: var(--mw-coral);
}

.review-detail__header {
  display: flex;
  gap: 16px;
  align-items: flex-start;
}

.review-detail__cover {
  width: 60px;
  height: 84px;
  border-radius: 8px;
  flex-shrink: 0;
  object-fit: cover;
}

.review-detail__info {
  flex: 1;
}

.review-detail__anime-name {
  font-size: 18px;
  font-weight: 700;
  color: var(--mw-text);
  margin: 0 0 8px 0;
}

.review-detail__user {
  font-size: 13px;
  color: var(--mw-text-soft);
  margin: 0 0 4px 0;
}

.review-detail__time {
  font-size: 12px;
  color: var(--mw-text-muted);
  margin: 0;
}

.review-detail__content {
  font-size: 14px;
  color: var(--mw-text);
  line-height: 1.8;
  white-space: pre-wrap;
  word-break: break-word;
}

/* 弹窗样式，确保全屏显示 */
:deep(.review-detail-dialog) {
  display: flex;
  align-items: center !important;
  justify-content: center !important;
}

:deep(.review-detail-dialog .el-dialog) {
  margin: 0 auto !important;
  max-height: 90vh;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

:deep(.review-detail-dialog .el-dialog__body) {
  overflow-y: auto;
  flex: 1;
  padding: 20px 24px;
}
</style>
