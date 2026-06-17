import request from './request';
import type { RecommendResult, RecommendApiResponse } from '../types/api';

/**
 * 获取个性化推荐。
 *
 * 返回标准 Result<T> 包装的 JSON（与项目其他接口一致）：
 *   { code: 200, message: "操作成功", data: RecommendResult, isSuccess: true }
 * 调用方需通过 .data 取出 RecommendResult。
 */
export async function getRecommendations(params: {
  keyword?: string;
  animeType?: string;
  topK?: number;
  deterministic?: boolean;
}): Promise<RecommendResult> {
  const res = await request.get('/recommendations', { params }) as RecommendApiResponse;
  return res.data;
}
