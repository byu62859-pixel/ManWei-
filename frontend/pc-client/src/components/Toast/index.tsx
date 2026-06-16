import { createPortal } from 'react-dom';
import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import styles from './Toast.module.css';

/* ---- types ---- */

interface ToastItem {
  id: number;
  message: ReactNode;
  dot: string; // CSS color for the status dot
}

interface ToastCtx {
  push(msg: ReactNode, dot: string, ms: number): void;
}

const Ctx = createContext<ToastCtx | null>(null);

/* ---- provider ---- */

let _nextId = 0;
const MAX = 3;

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([]);
  const timers = useRef<Map<number, ReturnType<typeof setTimeout>>>(new Map());

  const push = useCallback((message: ReactNode, dot: string, ms: number) => {
    const id = ++_nextId;
    setItems(prev => {
      const next = [...prev, { id, message, dot }];
      if (next.length > MAX) next.shift();
      return next;
    });
    const timer = setTimeout(() => {
      // mark as leaving first for the exit animation
      setItems(prev => prev.map(t => (t.id === id ? { ...t, id: t.id } : t)));
      const leavingEl = document.querySelector(`[data-toast-id="${id}"]`);
      if (leavingEl) {
        leavingEl.classList.add(styles.leaving);
        setTimeout(() => setItems(prev => prev.filter(t => t.id !== id)), 220);
      } else {
        setItems(prev => prev.filter(t => t.id !== id));
      }
      timers.current.delete(id);
    }, ms);
    timers.current.set(id, timer);
  }, []);

  // cleanup on unmount
  useEffect(() => () => { timers.current.forEach(t => clearTimeout(t)); }, []);

  return (
    <Ctx.Provider value={{ push }}>
      {children}
      {createPortal(
        <div className={styles.portal}>
          {items.map(t => (
            <div key={t.id} className={styles.toast} data-toast-id={t.id}>
              <span className={styles.dot} style={{ background: t.dot }} />
              <span>{t.message}</span>
            </div>
          ))}
        </div>,
        document.body,
      )}
    </Ctx.Provider>
  );
}

/* ---- hook ---- */

export function useToast() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error('useToast must be used within ToastProvider');
  return ctx;
}
