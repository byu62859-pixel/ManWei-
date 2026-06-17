import { useEffect, useRef, useState } from 'react';
import { Drawer, Input, Button, Empty } from 'antd';
import { CloseOutlined, SendOutlined } from '@ant-design/icons';
import Markdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { AiAssistantIcon } from '../AiAssistantIcon';
import { RecommendAnimeCard } from '../RecommendAnimeCard';
import { useAiAssistantStore } from '../../stores/aiAssistantStore';
import { streamChat } from '../../services/chat';
import type { ChatMessage, RecommendResult } from '../../types/api';
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
  const [input, setInput] = useState('');
  const abortRef = useRef<AbortController | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // 关闭时中止正在进行的请求, 但保留消息记录
  const handleClose = () => {
    abortRef.current?.abort();
    setInput('');
    closeDrawer();
  };

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
          <AiAssistantIcon style={{ width: 28, height: 28, fontSize: 28 }} />
          <span>AI 助手</span>
          <span className={styles.statusDot} data-streaming={isStreaming} />
        </div>
      }
      placement="right"
      size={480}
      open={isOpen}
      onClose={handleClose}
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
            {error}
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
      <div className={styles.bubbleContent}>
        {isUser
          ? message.content
          : (
            <Markdown
              remarkPlugins={[remarkGfm]}
              components={{
                p: ({ children }) => <p className={styles.mdParagraph}>{children}</p>,
                table: ({ children }) => <table className={styles.mdTable}>{children}</table>,
                th: ({ children }) => <th className={styles.mdTh}>{children}</th>,
                td: ({ children }) => <td className={styles.mdTd}>{children}</td>,
              }}
            >
              {message.content}
            </Markdown>
          )
        }
        {message.isStreaming && <span className={styles.cursor}>&#x25CD;</span>}
        {message.isStreaming && message.toolCalls?.map(tc => (
          <details key={tc.id} className={styles.toolCall}>
            <summary>正在查询...</summary>
            <pre>{(() => {
              try { return JSON.stringify(JSON.parse(tc.resultJson || '{}'), null, 2); }
              catch { return tc.resultJson || '{}'; }
            })()}</pre>
          </details>
        ))}
        {!message.isStreaming && message.toolCalls
          ?.filter(tc => tc.name === 'recommend_anime' && tc.resultJson)
          .map(tc => {
            let rec: RecommendResult | null = null;
            try { rec = JSON.parse(tc.resultJson); } catch { /* fallback to raw below */ }
            if (!rec?.items?.length) return null;
            return (
              <div key={tc.id} className={styles.toolRecCardBlock}>
                <div className={styles.toolRecLabel}>
                  {rec.mode === 'popular' ? '热门推荐' : '为你推荐'}
                </div>
                {rec.items.slice(0, 3).map((it, idx) => (
                  <RecommendAnimeCard
                    key={it.bangumiId ?? `rec-${idx}`}
                    item={it}
                    mode={rec.mode}
                    compact
                    onClick={(item) => {
                      if (item.animeId) window.open(`/anime/${item.animeId}`, '_blank');
                    }}
                  />
                ))}
              </div>
            );
          })}
        {message.isError && (
          <div className={styles.messageError}>连接中断，以上内容可能不完整</div>
        )}
      </div>
    </div>
  );
}
