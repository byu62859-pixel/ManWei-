import axios from 'axios';

const request = axios.create({
  baseURL: '/api',
  timeout: 30000,
});

request.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('mw_token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    console.log('Axios request:', config.method?.toUpperCase(), config.url, config.params);
    return config;
  },
  (error) => Promise.reject(error)
);

request.interceptors.response.use(
  (response) => {
    console.log('Axios response:', response.config.url, response.status, response.data?.data?.page);
    return response.data;
  },
  (error) => {
    if (error.response?.status === 401 && window.location.pathname !== '/login') {
      localStorage.removeItem('mw_token');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default request as typeof axios & {
  post: <T = any>(url: string, data?: any, config?: any) => Promise<T>;
  get: <T = any>(url: string, params?: any) => Promise<T>;
  put: <T = any>(url: string, data?: any, config?: any) => Promise<T>;
  delete: <T = any>(url: string, params?: any) => Promise<T>;
};
