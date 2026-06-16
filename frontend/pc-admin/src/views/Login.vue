<template>
  <div class="login-page">
    <!-- 装饰性背景元素 -->
    <div class="bg-decor bg-decor--1"></div>
    <div class="bg-decor bg-decor--2"></div>
    <div class="bg-decor bg-decor--3"></div>

    <!-- 背景网格纹理 -->
    <div class="bg-grid"></div>

    <!-- 登录卡片 -->
    <div class="login-card">
      <!-- Logo 区域 -->
      <div class="login-logo">
        <span class="login-brand__name">ManWei Admin</span>
      </div>

      <!-- 标题 -->
      <div class="login-header">
        <h1 class="login-title">欢迎回来</h1>
        <p class="login-subtitle">登录到你的管理后台</p>
      </div>

      <!-- 表单 -->
      <el-form
        ref="formRef"
        :model="form"
        :rules="rules"
        @submit.prevent="handleLogin"
      >
        <el-form-item prop="username" class="form-item">
          <div class="input-group">
            <div class="input-icon">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/>
                <circle cx="12" cy="7" r="4"/>
              </svg>
            </div>
            <el-input
              v-model="form.username"
              placeholder="用户名"
              size="large"
              class="mw-input"
            />
          </div>
        </el-form-item>

        <el-form-item prop="password" class="form-item">
          <div class="input-group">
            <div class="input-icon">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <rect x="3" y="11" width="18" height="11" rx="2" ry="2"/>
                <path d="M7 11V7a5 5 0 0 1 10 0v4"/>
              </svg>
            </div>
            <el-input
              v-model="form.password"
              type="password"
              placeholder="密码"
              size="large"
              class="mw-input"
              @keyup.enter="handleLogin"
            />
          </div>
        </el-form-item>

        <el-form-item class="form-item form-item--submit">
          <el-button
            type="primary"
            size="large"
            :loading="loading"
            class="login-btn"
            @click="handleLogin"
          >
            <span v-if="!loading">登 录</span>
            <span v-else class="loading-dots">
              <span></span><span></span><span></span>
            </span>
          </el-button>
        </el-form-item>
      </el-form>

      <!-- 底部信息 -->
      <div class="login-footer">
        <span>动漫情感管理平台</span>
        <span class="login-footer__sep">·</span>
        <span>v2.0</span>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const formRef = ref(null)
const loading = ref(false)

const form = reactive({
  username: '',
  password: ''
})

const rules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }]
}

const handleLogin = async () => {
  if (!formRef.value) return

  await formRef.value.validate(async (valid) => {
    if (!valid) return

    loading.value = true
    try {
      await authStore.login(form.username, form.password)
      router.push('/dashboard')
    } catch (error) {
      ElMessage.error(error.message || '登录失败，请检查用户名和密码')
    } finally {
      loading.value = false
    }
  })
}
</script>

<style scoped>
.login-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #FFF8F6 0%, #FFF0EC 30%, #F5EDE8 60%, #F0E8E3 100%);
  position: relative;
  overflow: hidden;
}

/* 装饰圆形 */
.bg-decor {
  position: absolute;
  border-radius: 50%;
  pointer-events: none;
}

.bg-decor--1 {
  width: 600px;
  height: 600px;
  background: radial-gradient(circle, rgba(255, 94, 98, 0.09) 0%, rgba(255, 94, 98, 0.04) 40%, transparent 70%);
  top: -280px;
  right: -180px;
  animation: mw-float 12s ease-in-out infinite;
}

.bg-decor--2 {
  width: 500px;
  height: 500px;
  background: radial-gradient(circle, rgba(155, 110, 243, 0.09) 0%, rgba(155, 110, 243, 0.04) 40%, transparent 70%);
  bottom: -220px;
  left: -160px;
  animation: mw-float 15s ease-in-out infinite reverse;
}

.bg-decor--3 {
  width: 350px;
  height: 350px;
  background: radial-gradient(circle, rgba(78, 204, 163, 0.07) 0%, rgba(78, 204, 163, 0.03) 40%, transparent 70%);
  top: 30%;
  left: 8%;
  animation: mw-float 10s ease-in-out infinite;
}

.bg-decor--4 {
  width: 220px;
  height: 220px;
  background: radial-gradient(circle, rgba(255, 94, 98, 0.07) 0%, transparent 70%);
  top: 18%;
  right: 18%;
  animation: mw-float 9s ease-in-out infinite 2s;
}

/* 网格纹理 */
.bg-grid {
  position: absolute;
  inset: 0;
  background-image:
    linear-gradient(rgba(45, 53, 97, 0.025) 1px, transparent 1px),
    linear-gradient(90deg, rgba(45, 53, 97, 0.025) 1px, transparent 1px);
  background-size: 48px 48px;
  pointer-events: none;
}

/* 登录卡片 */
.login-card {
  position: relative;
  z-index: 1;
  width: 440px;
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border: 1px solid rgba(255, 255, 255, 0.8);
  border-radius: 28px;
  padding: 52px 48px;
  box-shadow:
    0 32px 80px rgba(45, 53, 97, 0.10),
    0 8px 32px rgba(45, 53, 97, 0.06),
    inset 0 1px 0 rgba(255, 255, 255, 0.9);
  animation: mw-scaleIn var(--mw-dur-slow) var(--mw-ease-spring) forwards;
  opacity: 0;
}

/* Logo */
.login-logo {
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 32px;
  opacity: 0;
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) 100ms forwards;
}

.login-brand__name {
  font-family: 'Sora', 'Noto Sans SC', sans-serif;
  font-size: 28px;
  font-weight: 700;
  color: var(--mw-text);
  letter-spacing: 0.08em;
  line-height: 1.2;
}

/* 标题 */
.login-header {
  text-align: center;
  margin-bottom: 36px;
  opacity: 0;
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) 200ms forwards;
}

.login-title {
  font-family: 'Sora', 'Noto Sans SC', sans-serif;
  font-size: 28px;
  font-weight: 700;
  color: var(--mw-text);
  margin-bottom: 8px;
  letter-spacing: -0.02em;
}

.login-subtitle {
  font-size: 14px;
  color: var(--mw-text-muted);
  font-weight: 500;
}

/* 表单 */
:deep(.el-form) {
  width: 100%;
}

:deep(.el-form-item) {
  margin-bottom: 20px;
}

:deep(.el-form-item__error) {
  font-size: 12px;
  padding-top: 4px;
}

.input-group {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
}

.input-icon {
  position: absolute;
  left: 14px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--mw-text-muted);
  z-index: 1;
  transition: color var(--mw-dur-fast) var(--mw-ease);
  pointer-events: none;
}

:deep(.mw-input .el-input__wrapper) {
  padding-left: 46px;
  height: 54px;
  border-radius: 12px;
  background: var(--mw-cream);
  border: 1.5px solid var(--mw-border);
  transition: all var(--mw-dur-fast) var(--mw-ease) !important;
}

:deep(.mw-input .el-input__wrapper:hover) {
  border-color: var(--mw-peach);
}

:deep(.mw-input .el-input__wrapper:focus-within) {
  border-color: var(--mw-coral);
  box-shadow: 0 0 0 4px rgba(255, 94, 98, 0.08) !important;
  background: #fff;
}

:deep(.mw-input .el-input__wrapper:focus-within + .input-icon) {
  color: var(--mw-coral);
}

:deep(.mw-input .el-input__inner) {
  font-size: 15px;
  color: var(--mw-text);
}

/* 提交按钮 */
.form-item--submit {
  margin-top: 28px;
  margin-bottom: 0;
}

.login-btn {
  width: 100%;
  height: 54px;
  border-radius: 12px !important;
  font-size: 16px;
  font-weight: 700;
  letter-spacing: 0.08em;
  font-family: 'Sora', 'Noto Sans SC', sans-serif;
  background: linear-gradient(135deg, #FF5E62 0%, #FF7A70 50%, #FF8A75 100%) !important;
  border: none !important;
  box-shadow: 0 8px 32px rgba(255, 94, 98, 0.30), 0 4px 12px rgba(255, 94, 98, 0.20) !important;
  transition: transform var(--mw-dur-fast) var(--mw-ease-spring),
              box-shadow var(--mw-dur-fast) var(--mw-ease) !important;
}

.login-btn:hover {
  transform: translateY(-3px);
  box-shadow: 0 12px 40px rgba(255, 94, 98, 0.40), 0 6px 16px rgba(255, 94, 98, 0.25) !important;
}

.login-btn:active {
  transform: scale(0.97) !important;
  box-shadow: 0 4px 16px rgba(255, 94, 98, 0.25), 0 2px 8px rgba(255, 94, 98, 0.15) !important;
}

/* Loading dots */
.loading-dots {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
}

.loading-dots span {
  width: 6px;
  height: 6px;
  background: white;
  border-radius: 50%;
  animation: mw-dotPulse 1.4s ease-in-out infinite;
}

.loading-dots span:nth-child(2) { animation-delay: 0.2s; }
.loading-dots span:nth-child(3) { animation-delay: 0.4s; }

@keyframes mw-dotPulse {
  0%, 80%, 100% { transform: scale(0.6); opacity: 0.5; }
  40% { transform: scale(1); opacity: 1; }
}

/* 底部信息 */
.login-footer {
  text-align: center;
  margin-top: 36px;
  font-size: 12px;
  color: var(--mw-text-muted);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  opacity: 0;
  animation: mw-fadeSlideUp var(--mw-dur-slow) var(--mw-ease-out) 300ms forwards;
}

.login-footer__sep {
  opacity: 0.4;
}
</style>