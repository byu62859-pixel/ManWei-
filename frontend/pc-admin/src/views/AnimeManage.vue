<template>
  <div class="anime-manage-page">
    <!-- 页面标题 -->
    <div class="page-header">
      <div class="page-header__left">
        <h2 class="page-title">动漫管理</h2>
        <p class="page-subtitle">管理平台全部动漫数据</p>
      </div>
      <div class="page-header__right">
        <el-button type="primary" class="add-btn" @click="handleAdd">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
            <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
          </svg>
          新增动漫
        </el-button>
      </div>
    </div>

    <!-- 工具栏 -->
    <div class="toolbar">
      <el-input
        v-model="syncBangumiId"
        placeholder="Bangumi ID"
        clearable
        class="sync-input"
      >
        <template #append>
          <el-button class="sync-btn" @click="handleSync">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 2v6h-6M3 12a9 9 0 0 1 15-6.7L21 8M3 22v-6h6M21 12a9 9 0 0 1-15 6.7L3 16"/>
            </svg>
            同步
          </el-button>
        </template>
      </el-input>
      <transition name="mw-fade">
        <el-button
          v-if="selectedAnimes.length > 0"
          class="batch-delete-btn"
          @click="handleBatchDelete"
        >
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
          </svg>
          已选 {{ selectedAnimes.length }} 项 · 批量删除
        </el-button>
      </transition>
    </div>

    <!-- 搜索栏 -->
    <el-row :gutter="16" class="search-bar">
      <el-col :span="8">
        <el-input
          v-model="keyword"
          placeholder="搜索动漫名称..."
          prefix-icon="Search"
          clearable
          @keyup.enter="handleSearch"
          @clear="handleSearch"
          class="search-input"
        />
      </el-col>
      <el-col :span="4">
        <el-select v-model="typeFilter" placeholder="类型筛选" clearable @change="handleSearch" class="type-select">
          <el-option label="全部" value="" />
          <el-option label="TV" value="TV" />
          <el-option label="剧场版" value="剧场版" />
          <el-option label="OVA" value="OVA" />
        </el-select>
      </el-col>
      <el-col :span="4">
        <el-button type="primary" class="search-btn" @click="handleSearch">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
            <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
          </svg>
          查询
        </el-button>
      </el-col>
    </el-row>

    <!-- 动漫表格 -->
    <el-card class="table-card">
      <el-table :data="animeList" v-loading="loading" stripe @row-click="handleRowClick" @selection-change="handleSelectionChange">
        <el-table-column type="selection" width="48" />
        <el-table-column prop="id" label="ID" width="60" />
        <el-table-column prop="cover" label="封面" width="70">
          <template #default="{ row }">
            <img
              v-if="row.cover"
              :src="row.cover"
              class="cover-thumb"
              loading="lazy"
              @click="openPreview(row.cover)"
            />
            <div v-else class="cover-placeholder-thumb">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                <rect x="2" y="2" width="20" height="20" rx="3"/>
                <circle cx="8" cy="8" r="1.5"/>
                <path d="M21 15l-5-5L5 20"/>
              </svg>
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="name" label="名称" min-width="140" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="anime-name">{{ row.name }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="bangumiId" label="Bangumi" width="90">
          <template #default="{ row }">
            <span v-if="row.bangumiId" class="bangumi-id">{{ row.bangumiId }}</span>
            <span v-else class="no-id">-</span>
          </template>
        </el-table-column>
        <el-table-column prop="animeType" label="类型" width="80">
          <template #default="{ row }">
            <span class="type-chip">{{ row.animeType }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="favoriteCount" label="收藏" width="70" sortable>
          <template #default="{ row }">
            <span class="stat-num">{{ row.favoriteCount || 0 }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="avgRating" label="均分" width="60" sortable>
          <template #default="{ row }">
            <span class="stat-score" v-if="row.avgRating">{{ (row.avgRating).toFixed(1) }}</span>
            <span class="stat-none" v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column prop="reviewCount" label="观后感" width="70" sortable>
          <template #default="{ row }">
            <span class="stat-num">{{ row.reviewCount || 0 }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="summary" label="简介" min-width="150" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="summary-text">{{ row.summary || '-' }}</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="120" fixed="right">
          <template #default="{ row }">
            <div class="op-btn-group">
              <el-button size="small" class="op-btn op-btn--edit" @click.stop="handleEdit(row)">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                  <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                </svg>
              </el-button>
              <el-button size="small" class="op-btn op-btn--delete" @click.stop="handleDelete(row)">
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

    <!-- 新增/编辑弹窗 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogMode === 'create' ? '新增动漫' : '编辑动漫'"
      width="500px"
      class="mw-dialog"
      @close="handleDialogClose"
    >
      <el-form :model="formData" label-width="80px" class="mw-form">
        <el-form-item label="名称" required>
          <el-input v-model="formData.name" placeholder="请输入动漫名称" />
        </el-form-item>
        <el-form-item label="封面">
          <el-input v-model="formData.cover" placeholder="请输入封面图片 URL" />
        </el-form-item>
        <el-form-item label="类型">
          <el-select v-model="formData.animeType" placeholder="请选择类型" style="width: 100%;">
            <el-option label="TV" value="TV" />
            <el-option label="剧场版" value="剧场版" />
            <el-option label="OVA" value="OVA" />
          </el-select>
        </el-form-item>
        <el-form-item label="简介">
          <el-input
            v-model="formData.summary"
            type="textarea"
            :rows="3"
            placeholder="请输入简介"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>

    <!-- 封面全屏预览弹窗 - 自定义玻璃质感 -->
    <Teleport to="body">
      <div v-if="previewVisible" class="img-preview-overlay" @click="previewVisible = false">
        <div class="img-preview-content" @click.stop>
          <button class="img-preview-close" @click="previewVisible = false">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
              <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
            </svg>
          </button>
          <img :src="previewImage" class="img-preview-img" />
        </div>
      </div>
    </Teleport>

    <!-- 情感标签词云弹窗 -->
    <el-dialog
      v-model="wordCloudVisible"
      :title="'「 ' + wordCloudAnimeName + ' 」情感标签词云'"
      width="520px"
      class="mw-dialog"
    >
      <div v-if="wordCloudLoading" class="wordcloud-loading">
        <el-skeleton animated />
      </div>
      <div v-else-if="wordCloudData.length > 0" class="wordcloud-content">
        <div class="wordcloud-list">
          <span
            v-for="item in wordCloudData"
            :key="item.name"
            class="wordcloud-tag"
            :style="getWordStyle(item.count, wordCloudMaxCount)"
          >{{ item.name }}</span>
        </div>
      </div>
      <div v-else class="wordcloud-empty">
        <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
          <path d="M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82z"/>
          <line x1="7" y1="7" x2="7.01" y2="7"/>
        </svg>
        <p>暂无用户自定义标签</p>
      </div>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getAnimeList, syncAnime, createAnime, updateAnime, deleteAnime, batchDeleteAnime, getAnimeWordCloud } from '@/api/anime'

const loading = ref(true)
const animeList = ref([])
const keyword = ref('')
const typeFilter = ref('')
const syncBangumiId = ref('')

const pagination = reactive({
  page: 1,
  pageSize: 20,
  total: 0
})

const dialogVisible = ref(false)
const dialogMode = ref('create')

// 批量删除选中
const selectedAnimes = ref([])
const formData = reactive({
  id: null,
  name: '',
  cover: '',
  summary: '',
  animeType: 'TV'
})

const previewVisible = ref(false)
const previewImage = ref('')

// 词云弹窗
const wordCloudVisible = ref(false)
const wordCloudAnimeName = ref('')
const wordCloudAnimeId = ref(null)
const wordCloudLoading = ref(false)
const wordCloudData = ref([])
const wordCloudMaxCount = ref(1)

const openPreview = (url) => {
  previewImage.value = url
  previewVisible.value = true
}

const formatDate = (dateStr) => {
  if (!dateStr) return '-'
  const d = new Date(dateStr)
  return d.toLocaleDateString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const loadAnime = async () => {
  loading.value = true
  try {
    const result = await getAnimeList({
      page: pagination.page,
      pageSize: pagination.pageSize,
      keyword: keyword.value || undefined,
      type: typeFilter.value || undefined
    })
    animeList.value = result.items
    pagination.total = result.totalCount
  } catch (error) {
    ElMessage.error(error.message || '加载动漫列表失败')
  } finally {
    loading.value = false
  }
}

const handleSync = async () => {
  const id = syncBangumiId.value.trim()
  if (!id) {
    ElMessage.warning('请输入 Bangumi ID')
    return
  }
  try {
    await syncAnime(parseInt(id, 10))
    ElMessage.success('同步成功')
    syncBangumiId.value = ''
    await loadAnime()
  } catch (error) {
    ElMessage.error(error.message || '同步失败')
  }
}

const handleSearch = () => {
  pagination.page = 1
  loadAnime()
}

const handleSizeChange = (size) => {
  pagination.pageSize = size
  pagination.page = 1
  loadAnime()
}

const handleCurrentChange = (page) => {
  pagination.page = page
  loadAnime()
}

const handleAdd = () => {
  dialogMode.value = 'create'
  resetForm()
  dialogVisible.value = true
}

const handleEdit = (row) => {
  dialogMode.value = 'edit'
  formData.id = row.id
  formData.name = row.name
  formData.cover = row.cover || ''
  formData.summary = row.summary || ''
  formData.animeType = row.animeType || 'TV'
  dialogVisible.value = true
}

const resetForm = () => {
  formData.id = null
  formData.name = ''
  formData.cover = ''
  formData.summary = ''
  formData.animeType = 'TV'
}

const handleDialogClose = () => {
  resetForm()
}

const handleSave = async () => {
  if (!formData.name || !formData.name.trim()) {
    ElMessage.warning('请输入动漫名称')
    return
  }
  try {
    const payload = {
      name: formData.name.trim(),
      cover: formData.cover.trim() || null,
      summary: formData.summary.trim() || null,
      animeType: formData.animeType || 'TV'
    }
    if (dialogMode.value === 'create') {
      await createAnime(payload)
      ElMessage.success('新增成功')
    } else {
      await updateAnime(formData.id, payload)
      ElMessage.success('更新成功')
    }
    dialogVisible.value = false
    await loadAnime()
  } catch (error) {
    ElMessage.error(error.message || '保存失败')
  }
}

const handleDelete = async (row) => {
  try {
    await ElMessageBox.confirm(
      `确定删除动漫"${row.name}"吗？此操作不可恢复。`,
      '确认删除',
      { type: 'warning' }
    )
    await deleteAnime(row.id)
    ElMessage.success('删除成功')
    await loadAnime()
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error.message || '删除失败')
    }
  }
}

const handleSelectionChange = (rows) => {
  selectedAnimes.value = rows
}

const handleBatchDelete = async () => {
  const rows = selectedAnimes.value
  if (rows.length === 0) return

  try {
    await ElMessageBox.confirm(
      `确定批量删除 ${rows.length} 个动漫吗？\n\n将级联删除这些动漫及其所有用户的收藏、观后感、情绪记录，且不可恢复。`,
      '确认批量删除',
      { type: 'warning', confirmButtonText: '确认删除', cancelButtonText: '取消' }
    )
  } catch {
    return  // 用户取消
  }

  try {
    const ids = rows.map(r => r.id)
    const result = await batchDeleteAnime(ids)
    // 后端返回 Result<T> 包装,实际数据在 .data
    const data = result?.data || result || {}
    const deleted = data.deleted ?? 0
    const notFound = data.notFound ?? 0
    const errorCount = data.errors?.length ?? 0
    const truncated = data.errorsTruncated ?? false

    const lines = [`成功删除 ${deleted} 个动漫`]
    if (notFound > 0) lines.push(`${notFound} 个 ID 不存在（可能已被其他管理员删除）`)
    if (errorCount > 0) {
      lines.push(`${errorCount} 个错误${truncated ? '（还有更多错误未显示，请查看服务端日志）' : ''}`)
    }

    if (deleted > 0) {
      ElMessage.success(lines.join('\n'))
    } else if (notFound > 0 || errorCount > 0) {
      ElMessage.warning(lines.join('\n'))
    }

    selectedAnimes.value = []
    await loadAnime()
  } catch (error) {
    ElMessage.error(error.message || '批量删除失败')
  }
}

const handleRowClick = async (row) => {
  wordCloudAnimeId.value = row.id
  wordCloudAnimeName.value = row.name
  wordCloudVisible.value = true
  wordCloudLoading.value = true
  wordCloudData.value = []
  try {
    const res = await getAnimeWordCloud(row.id)
    wordCloudData.value = res || []
    wordCloudMaxCount.value = Math.max(...wordCloudData.value.map(d => d.count), 1)
  } catch (e) {
    wordCloudData.value = []
  } finally {
    wordCloudLoading.value = false
  }
}

const getWordStyle = (count, maxCount) => {
  const minSize = 14, maxSize = 36
  const ratio = maxCount > 1 ? (count - 1) / (maxCount - 1) : 0
  const fontSize = Math.round(minSize + (maxSize - minSize) * ratio)
  const opacity = 0.5 + 0.5 * ratio
  const hue = 350 - Math.round(ratio * 30)
  return `font-size: ${fontSize}px; color: hsl(${hue}, 75%, 55%); opacity: ${opacity};`
}

onMounted(() => {
  loadAnime()
})
</script>

<style scoped>
.anime-manage-page {
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) forwards;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  flex-shrink: 0;
}
.page-header__left {
  opacity: 0;
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) forwards;
}
.page-title {
  font-size: 22px;
  font-weight: 700;
  color: var(--mw-text);
  margin-bottom: 4px;
}
.page-subtitle {
  font-size: 13px;
  color: var(--mw-text-muted);
}
.page-header__right {
  opacity: 0;
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) 100ms forwards;
}

.add-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border-radius: 10px !important;
}

.toolbar {
  margin-bottom: 12px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: 12px;
}
.sync-input {
  width: 240px;
}
.sync-btn {
  display: flex;
  align-items: center;
  gap: 5px;
  font-size: 13px;
}
.batch-delete-btn {
  display: inline-flex !important;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  border-radius: 10px !important;
  background: rgba(255, 94, 98, 0.12) !important;
  border: 1px solid rgba(255, 94, 98, 0.30) !important;
  color: #FF5E62 !important;
  transition: all var(--mw-dur-fast) !important;
}
.batch-delete-btn:hover {
  background: rgba(255, 94, 98, 0.22) !important;
  border-color: rgba(255, 94, 98, 0.45) !important;
  box-shadow: 0 2px 8px rgba(255, 94, 98, 0.25) !important;
}

.search-bar {
  margin-bottom: 12px;
  flex-shrink: 0;
}
.search-input {
  width: 100%;
}
.type-select {
  width: 100%;
}
.search-btn {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  border-radius: 10px !important;
}

.table-card {
  border-radius: 16px;
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

:deep(.el-table) {
  flex: 1;
}

:deep(.el-table__body-wrapper) {
  overflow-y: auto;
}

:deep(.el-table) {
  font-size: 14px;
}

.cover-thumb {
  width: 50px;
  height: 66px;
  border-radius: 6px;
  object-fit: cover;
  display: block;
  cursor: pointer;
  transition: transform var(--mw-dur-fast) var(--mw-ease-bounce);
}
.cover-thumb:hover {
  transform: scale(1.08);
  box-shadow: var(--mw-shadow-md);
}

.cover-placeholder-thumb {
  width: 50px;
  height: 66px;
  border-radius: 6px;
  background: var(--mw-cream-deep);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--mw-text-muted);
}

.anime-name {
  font-weight: 600;
  color: var(--mw-text);
}

.bangumi-id {
  font-family: 'Courier New', monospace;
  font-size: 12px;
  color: var(--mw-text-soft);
  background: var(--mw-cream-deep);
  padding: 2px 6px;
  border-radius: 4px;
}

.no-id {
  color: var(--mw-text-muted);
}

.type-chip {
  display: inline-flex;
  align-items: center;
  padding: 3px 10px;
  background: var(--mw-lavender-soft);
  color: #7050d0;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 500;
}

.summary-text {
  color: var(--mw-text-soft);
  font-size: 13px;
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

/* 编辑按钮 - 蓝色系 */
.op-btn--edit {
  background: rgba(79, 122, 220, 0.12) !important;
  border: 1px solid rgba(79, 122, 220, 0.25) !important;
  color: #4F7ADC !important;
}
.op-btn--edit:not(:disabled):hover {
  background: rgba(79, 122, 220, 0.22) !important;
  border-color: rgba(79, 122, 220, 0.40) !important;
  box-shadow: 0 2px 8px rgba(79, 122, 220, 0.25) !important;
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

.pagination-wrapper {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
  flex-shrink: 0;
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

/* 封面全屏预览 - 玻璃质感 */
.img-preview-overlay {
  position: fixed;
  inset: 0;
  z-index: 9999;
  background: rgba(20, 24, 45, 0.6);
  backdrop-filter: blur(16px) saturate(160%);
  -webkit-backdrop-filter: blur(16px) saturate(160%);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
}
.img-preview-content {
  position: relative;
  max-width: 90vw;
  max-height: 90vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
.img-preview-close {
  position: absolute;
  top: -48px;
  right: 0;
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.15);
  border: 1px solid rgba(255, 255, 255, 0.3);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: background 150ms, transform 150ms;
}
.img-preview-close:hover {
  background: rgba(255, 255, 255, 0.25);
  transform: scale(1.1);
}
.img-preview-img {
  max-width: 100%;
  max-height: 85vh;
  object-fit: contain;
  border-radius: 16px;
  box-shadow: 0 30px 80px rgba(0, 0, 0, 0.5), 0 0 0 1px rgba(255, 255, 255, 0.1) inset;
}
/* 统计单元格 */
.stat-num {
  font-size: 13px;
  font-weight: 600;
  color: var(--mw-text);
}
.stat-score {
  font-size: 13px;
  font-weight: 700;
  color: var(--mw-gold);
}
.stat-none {
  color: var(--mw-text-muted);
  font-size: 13px;
}

/* 词云 */
.wordcloud-loading {
  height: 200px;
  padding: 16px 0;
}
.wordcloud-content {
  min-height: 200px;
  padding: 8px 0;
}
.wordcloud-list {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  justify-content: center;
  align-items: center;
  padding: 16px;
}
.wordcloud-tag {
  display: inline-block;
  padding: 4px 12px;
  border-radius: 20px;
  background: var(--mw-cream-deep);
  cursor: default;
  transition: transform var(--mw-dur-fast) var(--mw-ease-bounce),
              background var(--mw-dur-fast);
  font-weight: 500;
  letter-spacing: 0.02em;
}
.wordcloud-tag:hover {
  transform: scale(1.1);
  background: var(--mw-coral-soft);
}
.wordcloud-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  height: 200px;
  color: var(--mw-text-muted);
}
.wordcloud-empty svg {
  opacity: 0.3;
}
.wordcloud-empty p {
  font-size: 14px;
}
</style>