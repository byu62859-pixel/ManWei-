import { createRouter, createWebHistory } from 'vue-router'
import { ElMessage } from 'element-plus'

const routes = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/Login.vue'),
    meta: { public: true }
  },
  {
    path: '/',
    component: () => import('@/layouts/DefaultLayout.vue'),
    children: [
      {
        path: '',
        redirect: '/dashboard'
      },
      {
        path: 'dashboard',
        name: 'Dashboard',
        component: () => import('@/views/Dashboard.vue'),
        meta: { requiresAuth: true }
      },
      {
        path: 'users',
        name: 'Users',
        component: () => import('@/views/UserManage.vue'),
        meta: { requiresAuth: true, roles: ['Admin'] }
      },
      {
        path: 'anime',
        name: 'Anime',
        component: () => import('@/views/AnimeManage.vue'),
        meta: { requiresAuth: true, roles: ['Admin'] }
      },
      {
        path: 'emotion-tags',
        name: 'EmotionTags',
        component: () => import('@/views/EmotionTagsManage.vue'),
        meta: { requiresAuth: true, roles: ['Admin'] }
      },
      {
        path: 'reviews',
        name: 'Reviews',
        component: () => import('@/views/ReviewsManage.vue'),
        meta: { requiresAuth: true, roles: ['Admin'] }
      }
    ]
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

// 路由守卫
router.beforeEach((to, from, next) => {
  const token = localStorage.getItem('token')
  const role = localStorage.getItem('role')

  // 有 token 访问 /login → 跳转 /dashboard
  if (to.path === '/login' && token) {
    return next('/dashboard')
  }

  // 无 token 访问需认证页 → 跳转 /login
  if (to.meta.requiresAuth && !token) {
    return next('/login')
  }

  // Admin 路由权限检查
  if (to.meta.roles && to.meta.roles.length > 0) {
    if (!to.meta.roles.includes(role)) {
      ElMessage.error('无权限')
      return next('/dashboard')
    }
  }

  next()
})

export default router
