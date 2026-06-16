<template>
  <div class="emotion-tags-page">
    <!-- 页面标题 -->
    <div class="page-header">
      <div class="page-header__left">
        <h2 class="page-title">情感标签管理</h2>
        <p class="page-subtitle">管理平台全部情感标签</p>
      </div>
      <div class="page-header__right">
        <div class="tag-stat">
          <span class="tag-stat__num">{{ pagination.total }}</span>
          <span class="tag-stat__label">个标签</span>
        </div>
      </div>
    </div>

    <!-- 工具栏 -->
    <div class="toolbar">
      <el-input
        v-model="keyword"
        placeholder="搜索标签名称..."
        prefix-icon="Search"
        clearable
        class="search-input"
        @input="handleSearch"
      />
    </div>

    <!-- 标签表格 -->
    <el-card class="table-card">
      <el-table :data="tags" v-loading="loading" stripe>
        <el-table-column prop="id" label="ID" width="80" />
        <el-table-column prop="name" label="标签名称" min-width="150">
          <template #default="{ row }">
            <div class="tag-name-cell">
              <span class="tag-badge" :class="row.isPreset ? 'tag-badge--preset' : 'tag-badge--custom'">
                {{ row.name }}
              </span>
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="isPreset" label="类型" width="100">
          <template #default="{ row }">
            <span class="type-badge" :class="row.isPreset ? 'type-badge--preset' : 'type-badge--custom'">
              {{ row.isPreset ? '预置' : '自定义' }}
            </span>
          </template>
        </el-table-column>
        <el-table-column prop="usageCount" label="使用次数" width="120" sortable>
          <template #default="{ row }">
            <span class="count-num">{{ row.usageCount || 0 }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="relatedUserCount" label="关联用户" width="120" sortable />
        <el-table-column prop="createTime" label="创建时间" width="180">
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
                :disabled="row.isPreset"
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
import { getTagStats, deleteTag } from '@/api/emotionTag'

const loading = ref(true)
const tags = ref([])
const keyword = ref('')

const pagination = reactive({
  page: 1,
  pageSize: 20,
  total: 0
})

const formatDate = (dateStr) => {
  if (!dateStr) return '-'
  const d = new Date(dateStr)
  return d.toLocaleDateString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit' })
}

const handleSearch = () => {
  pagination.page = 1
  loadTags()
}

const handleSizeChange = (size) => {
  pagination.pageSize = size
  pagination.page = 1
  loadTags()
}

const handleCurrentChange = (page) => {
  pagination.page = page
  loadTags()
}

const handleDelete = async (row) => {
  try {
    await ElMessageBox.confirm(
      `确定删除标签"${row.name}"吗？${row.isPreset ? '（预置标签不可删除）' : ''}`,
      '确认删除',
      { type: 'warning' }
    )
    await deleteTag(row.id)
    ElMessage.success('删除成功')
    await loadTags()
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error.message || '删除失败')
    }
  }
}

const loadTags = async () => {
  loading.value = true
  try {
    const result = await getTagStats({
      page: pagination.page,
      pageSize: pagination.pageSize,
      keyword: keyword.value || undefined
    })
    tags.value = result.items
    pagination.total = result.totalCount
  } catch (error) {
    ElMessage.error(error.message || '加载标签失败')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadTags()
})
</script>

<style scoped>
.emotion-tags-page {
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
.tag-stat {
  display: flex;
  align-items: baseline;
  gap: 4px;
  background: var(--mw-coral-soft);
  padding: 8px 16px;
  border-radius: 10px;
}
.tag-stat__num {
  font-size: 22px;
  font-weight: 700;
  color: var(--mw-coral);
  font-variant-numeric: tabular-nums;
}
.tag-stat__label {
  font-size: 12px;
  color: var(--mw-coral);
  opacity: 0.7;
}

.toolbar {
  margin-bottom: 16px;
}
.search-input {
  width: 320px;
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

.tag-name-cell {
  display: flex;
  align-items: center;
}
.tag-badge {
  display: inline-flex;
  align-items: center;
  padding: 4px 12px;
  border-radius: 6px;
  font-size: 13px;
  font-weight: 500;
}
.tag-badge--preset {
  background: var(--mw-lavender-soft);
  color: #7050d0;
}
.tag-badge--custom {
  background: var(--mw-coral-soft);
  color: #d94040;
}

.type-badge {
  display: inline-flex;
  align-items: center;
  padding: 3px 10px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 500;
}
.type-badge--preset {
  background: var(--mw-mint-soft);
  color: #3a9e5a;
}
.type-badge--custom {
  background: var(--mw-gold-soft);
  color: #b08800;
}

.count-num {
  font-variant-numeric: tabular-nums;
  font-weight: 600;
  color: var(--mw-text);
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