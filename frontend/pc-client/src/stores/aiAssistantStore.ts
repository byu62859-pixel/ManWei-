import { create } from 'zustand';
import type { ChatMessage, ChatToolCallItem } from '../types/api';

interface AiAssistantState {
  isOpen: boolean;
  messages: ChatMessage[];
  isStreaming: boolean;
  error: string | null;

  openDrawer: () => void;
  closeDrawer: () => void;
  toggleDrawer: () => void;

  appendUserMessage: (text: string) => string;
  startAssistantMessage: () => string;
  appendDelta: (id: string, content: string) => void;
  // 追加新的 tool_call (支持一轮多个)
  pushToolCall: (id: string, item: ChatToolCallItem) => void;
  // 用 tool_result 填充对应 toolCall 的 resultJson (按 id 匹配)
  patchToolCallResult: (id: string, toolCallId: string, resultJson: string) => void;
  finishAssistantMessage: (id: string) => void;
  setError: (err: string | null) => void;
  markAssistantError: (id: string) => void;
  reset: () => void;
}

export const useAiAssistantStore = create<AiAssistantState>((set) => ({
  isOpen: false,
  messages: [],
  isStreaming: false,
  error: null,

  openDrawer: () => set({ isOpen: true }),
  closeDrawer: () => set({ isOpen: false }),
  toggleDrawer: () => set(s => ({ isOpen: !s.isOpen })),

  appendUserMessage: (text) => {
    const id = crypto.randomUUID();
    set(s => ({
      messages: [...s.messages, {
        id, role: 'user', content: text,
      }],
      isStreaming: true,
      error: null,
    }));
    return id;
  },

  startAssistantMessage: () => {
    const id = crypto.randomUUID();
    set(s => ({
      messages: [...s.messages, {
        id, role: 'assistant', content: '', isStreaming: true,
      }],
    }));
    return id;
  },

  appendDelta: (id, content) => set(s => ({
    messages: s.messages.map(m =>
      m.id === id ? { ...m, content: m.content + content } : m
    ),
  })),

  pushToolCall: (id, item) => set(s => ({
    messages: s.messages.map(m => {
      if (m.id !== id) return m;
      const existing = m.toolCalls ?? [];
      // 同 id 重复时 (网络重发) 更新而非重复追加
      const idx = existing.findIndex(t => t.id === item.id);
      if (idx >= 0) {
        const next = existing.slice();
        next[idx] = { ...next[idx], ...item };
        return { ...m, toolCalls: next };
      }
      return { ...m, toolCalls: [...existing, item] };
    }),
  })),

  patchToolCallResult: (id, toolCallId, resultJson) => set(s => ({
    messages: s.messages.map(m => {
      if (m.id !== id || !m.toolCalls) return m;
      return {
        ...m,
        toolCalls: m.toolCalls.map(t =>
          t.id === toolCallId ? { ...t, resultJson } : t
        ),
      };
    }),
  })),

  finishAssistantMessage: (id) => set(s => ({
    messages: s.messages.map(m =>
      m.id === id ? { ...m, isStreaming: false } : m
    ),
    isStreaming: false,
  })),

  setError: (err) => set({ error: err }),

  markAssistantError: (id) => set(s => ({
    messages: s.messages.map(m =>
      m.id === id ? { ...m, isStreaming: false, isError: true } : m
    ),
    isStreaming: false,
  })),

  reset: () => set({
    messages: [],
    isStreaming: false,
    error: null,
  }),
}));
