import type { ChatStreamEvent } from '../types/api';

const API_BASE = '/api';

// 与 services/request.ts 拦截器保持一致: 直接从 localStorage 读 token,
// 避免 service 层依赖 Zustand store (防止循环依赖 + 字段名变动时静默失败)
const TOKEN_KEY = 'mw_token';

export interface StreamChatHandlers {
  onDelta: (content: string) => void;
  onToolCall: (e: Extract<ChatStreamEvent, { type: 'tool_call' }>) => void;
  onToolResult: (e: Extract<ChatStreamEvent, { type: 'tool_result' }>) => void;
  onDone: () => void;
  onError: (err: string) => void;
}

export async function streamChat(
  message: string,
  handlers: StreamChatHandlers,
  signal: AbortSignal,
): Promise<void> {
  const token = localStorage.getItem(TOKEN_KEY);
  if (!token) {
    handlers.onError('未登录');
    return;
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE}/pcaia/chat-stream`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
      body: JSON.stringify({ message, history: [] }),
      signal,
    });
  } catch (err) {
    handlers.onError(err instanceof Error ? err.message : '网络错误');
    return;
  }

  if (!response.ok) {
    if (response.status === 401) {
      handlers.onError('登录已过期, 请重新登录');
      // 与 request.ts 401 行为一致: 跳转登录页
      localStorage.removeItem(TOKEN_KEY);
      window.location.href = '/login';
      return;
    }
    handlers.onError(`HTTP ${response.status}`);
    return;
  }

  if (!response.body) {
    handlers.onError('No response body');
    return;
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder('utf-8');
  let buffer = '';

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      // 按行切分 NDJSON
      const lines = buffer.split('\n');
      buffer = lines.pop() ?? '';  // 最后一段可能不完整, 留到下次

      for (const line of lines) {
        if (!line.trim()) continue;
        let evt: ChatStreamEvent;
        try {
          evt = JSON.parse(line);
        } catch {
          continue;  // 忽略坏行
        }
        switch (evt.type) {
          case 'delta':
            handlers.onDelta(evt.content);
            break;
          case 'tool_call':
            handlers.onToolCall(evt);
            break;
          case 'tool_result':
            handlers.onToolResult(evt);
            break;
          case 'done':
            handlers.onDone();
            return;
          case 'error':
            handlers.onError(evt.error);
            return;
        }
      }
    }
    // 流自然结束但没有收到 done → 也算 done
    handlers.onDone();
  } catch (err) {
    if ((err as Error).name === 'AbortError') {
      handlers.onError('已取消');
    } else {
      handlers.onError(err instanceof Error ? err.message : '流读取错误');
    }
  }
}
