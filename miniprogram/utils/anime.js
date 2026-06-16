import { get, post, put, del } from './api'

// ========== 动漫相关 ==========

/**
 * 获取动漫列表（分页+关键词+类型筛选）
 * @param {Object} params - { page, pageSize, keyword, type }
 */
export const getAnimeList = (params) => get('/api/anime', params)

/**
 * 获取动漫详情
 * @param {number} id - 动漫ID
 */
export const getAnimeDetail = (id) => get(`/api/anime/${id}`)

// ========== 收藏相关 ==========

/**
 * 检查当前用户是否已收藏指定动漫
 * @param {number} animeId - 动漫ID
 */
export const checkFavorite = (animeId) => get(`/api/Favorites/check/${animeId}`)

/**
 * 获取收藏列表（分页）
 * @param {Object} params - { page, pageSize, status }
 */
export const getFavoriteList = (params) => get('/api/Favorites', params)

/**
 * 添加收藏（初始状态为"想看"）
 * @param {number} animeId - 动漫ID
 */
export const addFavorite = (animeId) => post('/api/Favorites', { animeId })

/**
 * 更新收藏状态或进度
 * @param {number} id - 收藏ID
 * @param {Object} data - { status, progress }
 */
export const updateFavorite = (id, data) => put(`/api/Favorites/${id}`, data)

/**
 * 删除收藏
 * @param {number} id - 收藏ID
 */
export const deleteFavorite = (id) => del(`/api/Favorites/${id}`)

// ========== 情感标签 ==========

/**
 * 获取情感标签列表（预置标签 + 当前用户对该动漫的自定义标签）
 * @param {number} animeId - 动漫ID
 */
export const getEmotionTags = (animeId) => get('/api/EmotionTags', { animeId })

/**
 * 获取当前用户在已收藏动漫中使用过的标签名（去重，F2 标签筛选专用）
 */
export const getUsedEmotionTags = () => get('/api/EmotionTags/used')

/**
 * 创建自定义情感标签
 * @param {Object} data - { name, animeId }
 */
export const createEmotionTag = (data) => post('/api/EmotionTags', data)

/**
 * 删除自定义情感标签
 * @param {number} id - 标签ID
 */
export const deleteEmotionTag = (id) => del(`/api/EmotionTags/${id}`)

// ========== 情感曲线 ==========

/**
 * 获取情感曲线数据（按集数升序）
 * @param {number} favoriteId - 收藏ID
 */
export const getEmotionCurves = (favoriteId) => get(`/api/EmotionCurves/${favoriteId}`)

/**
 * 创建或更新情感记录（Upsert）
 * @param {Object} data - { favoriteId, episode, emotionLevel }
 */
export const upsertEmotionCurve = (data) => post('/api/EmotionCurves', data)

// ========== 观后感 ==========

/**
 * 获取观后感
 * @param {number} favoriteId - 收藏ID
 */
export const getReview = (favoriteId) => get(`/api/Reviews/${favoriteId}`)

/**
 * 创建或更新观后感（Upsert）
 * @param {Object} data - { favoriteId, content }
 */
export const upsertReview = (data) => post('/api/Reviews', data)

/**
 * 获取观后感 Feed 列表（分页）
 * @param {Object} params - { page, pageSize }
 */
export const getReviewFeed = (params) => get('/api/Reviews', params)

// ========== 情感词云 ==========

/**
 * 获取情感词云数据（当前用户创建的自定义标签，按名称分组统计数量）
 */
export const getWordCloud = () => get('/api/EmotionTags/wordcloud')

// ========== 用户统计 ==========

/**
 * 获取当前用户追番统计数据
 */
export const getUserStats = () => get('/api/Users/me/stats')
