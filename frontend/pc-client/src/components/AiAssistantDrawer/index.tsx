import { useEffect, useRef, useState } from 'react';
import { Drawer, Input, Button, Empty } from 'antd';
import { CloseOutlined, SendOutlined } from '@ant-design/icons';
import { AiAssistantIcon } from '../AiAssistantIcon';
import { useAiAssistantStore } from '../../stores/aiAssistantStore';
import { streamChat } from '../../services/chat';
import type { ChatMessage } from '../../types/api';
import styles from './AiAssistantDrawer.module.css';

export function AiAssistantDrawer() {
  const isOpen = useAiAssistantStore(s => s.isOpen);
  const closeDrawer = useAiAssistantStore(s => s.closeDrawer);
  const messages = useAiAssistantStore(s => s.messages);
  const isStreaming = useAiAssistantStore(s => s.isStreaming);
  const error = useAiAssistantStore(s => s.error);
  const setError = useAiAssistantStore(s => s.setError);
  const appendUserMessage = useAiAssistantStore(s => s.appendUserMessage);
  const startAssistantMessage = useAiAssistantStore(s => s.startAssistantMessage);
  const appendDelta = useAiAssistantStore(s => s.appendDelta);
  const pushToolCall = useAiAssistantStore(s => s.pushToolCall);
  const patchToolCallResult = useAiAssistantStore(s => s.patchToolCallResult);
  const finishAssistantMessage = useAiAssistantStore(s => s.finishAssistantMessage);
  const markAssistantError = useAiAssistantStore(s => s.markAssistantError);
  const reset = useAiAssistantStore(s => s.reset);

  const [input, setInput] = useState('');
  const abortRef = useRef<AbortController | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // 关闭时清空消息 (用户已确认不持久化)
  useEffect(() => {
    if (!isOpen) {
      abortRef.current?.abort();
      reset();
      setInput('');
    }
  }, [isOpen, reset]);

  // 自动滚动到底部
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  async function handleSend() {
    const text = input.trim();
    if (!text || isStreaming) return;
    setInput('');
    setError(null);

    appendUserMessage(text);
    const assistantId = startAssistantMessage();
    const ctrl = new AbortController();
    abortRef.current = ctrl;

    await streamChat(
      text,
      {
        onDelta: (content) => appendDelta(assistantId, content),
        onToolCall: (e) => {
          pushToolCall(assistantId, {
            id: e.toolCallId,
            name: e.toolName,
            argsJson: e.toolArgsJson,
            resultJson: '',
          });
        },
        onToolResult: (e) => {
          patchToolCallResult(assistantId, e.toolCallId, e.toolResultJson);
        },
        onDone: () => finishAssistantMessage(assistantId),
        onError: (err) => {
          markAssistantError(assistantId);
          setError(err);
        },
      },
      ctrl.signal,
    );
  }

  return (
    <Drawer
      title={
        <div className={styles.drawerHeader}>
          <AiAssistantIcon style={{ fontSize: 22 }} />
          <span>AI 助手</span>
          <span className={styles.statusDot} data-streaming={isStreaming} />
        </div>
      }
      placement="right"
      size={480}
      open={isOpen}
      onClose={closeDrawer}
      closeIcon={<CloseOutlined />}
      styles={{ body: { padding: 0 } }}
    >
      <div className={styles.body}>
        <div className={styles.messageList}>
          {messages.length === 0 ? (
            <div className={styles.emptyWrap}>
              <Empty
                description={
                  <div className={styles.emptyText}>
                    问我关于你追番的任何问题。<br />
                    例如: "我最近看了什么番" / "我的平均评分是多少"
                  </div>
                }
              />
            </div>
          ) : (
            messages.map(m => <MessageBubble key={m.id} message={m} />)
          )}
          <div ref={messagesEndRef} />
        </div>

        {error && (
          <div className={styles.errorBanner}>
            ⚠️ {error}
          </div>
        )}

        <div className={styles.inputBar}>
          <Input.TextArea
            value={input}
            onChange={e => setInput(e.target.value)}
            onPressEnter={e => {
              if (!e.shiftKey) {
                e.preventDefault();
                handleSend();
              }
            }}
            placeholder="输入消息, Enter 发送, Shift+Enter 换行"
            autoSize={{ minRows: 1, maxRows: 4 }}
            disabled={isStreaming}
          />
          <Button
            type="primary"
            icon={<SendOutlined />}
            onClick={handleSend}
            disabled={!input.trim() || isStreaming}
            loading={isStreaming}
          >
            发送
          </Button>
        </div>
      </div>
    </Drawer>
  );
}

function MessageBubble({ message }: { message: ChatMessage }) {
  const isUser = message.role === 'user';
  return (
    <div className={`${styles.bubble} ${isUser ? styles.user : styles.assistant}`}>
      {!isUser && <div className={styles.role}>AI</div>}
      <div className={styles.bubbleContent}>
        {message.content}
        {message.isStreaming && <span className={styles.cursor}>▍</span>}
        {message.toolCalls?.map(tc => (
          <details key={tc.id} className={styles.toolCall}>
            <summary>🔧 调用了 {tc.name}</summary>
            <pre>{(() => {
              try { return JSON.stringify(JSON.parse(tc.resultJson || '{}'), null, 2); }
              catch { return tc.resultJson || '{}'; }
            })()}</pre>
          </details>
        ))}
        {message.isError && (
          <div className={styles.messageError}>⚠️ 连接中断, 以上内容可能不完整</div>
        )}
      </div>
    </div>
  );
}
