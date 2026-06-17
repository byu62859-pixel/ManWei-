import request from './request';
import type { RecommendResult } from '../types/api';

/**
 * 获取个性化推荐。
 *
 * ⚠️ 格式偏差：本接口返回裸 RecommendResult JSON（不带项目标准 Result<T> 包装）。
 *   request.ts 拦截器已 return response.data，所以本函数返回值就是 HTTP body 本身。
 *   不要加 .code === 200 判断——裸 JSON 里没有 code 字段。
 *   HTTP 错误（401/500）由拦截器统一处理。
 */
export async function getRecommendations(params: {
  keyword?: string;
  animeType?: string;
  topK?: number;
}): Promise<RecommendResult> {
  return request.get('/recommendations', { params }) as Promise<RecommendResult>;
}
