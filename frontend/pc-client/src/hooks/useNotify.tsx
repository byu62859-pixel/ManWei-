import { App } from 'antd';
import type { ReactNode } from 'react';

/* ---------- minimalist-ui dark toast — warm monochrome, frosted, ultra-diffuse ---------- */

const baseStyle: React.CSSProperties = {
  background: 'rgba(28, 28, 30, 0.88)',
  backdropFilter: 'blur(16px)',
  WebkitBackdropFilter: 'blur(16px)',
  borderRadius: 6,
  boxShadow: '0 1px 6px rgba(0,0,0,0.04)',
  border: '1px solid rgba(255,255,255,0.06)',
  fontFamily: "-apple-system, BlinkMacSystemFont, 'SF Pro Display', 'Segoe UI', sans-serif",
  fontSize: 13,
  fontWeight: 400,
  lineHeight: 1.5,
  padding: '10px 14px',
};

const msgStyle: React.CSSProperties = { color: '#fff' };

/* ---------- colored dot icons ---------- */

const Dot = ({ color }: { color: string }) => (
  <span
    style={{
      display: 'inline-block',
      width: 8,
      height: 8,
      borderRadius: '50%',
      background: color,
      flexShrink: 0,
    }}
  />
);

const iconSuccess: ReactNode = <Dot color="#D4A574" />;
const iconError: ReactNode = <Dot color="#ef4444" />;
const iconWarning: ReactNode = <Dot color="#D4A574" />;

/* ---------- hook ---------- */

function WhiteMsg(msg: string) {
  return <span style={msgStyle}>{msg}</span>;
}

export function useNotify() {
  const { notification } = App.useApp();

  const opt = {
    placement: 'bottomRight' as const,
    className: 'dark-toast',
    style: baseStyle,
    closeIcon: null as ReactNode,
  };

  return {
    success(msg: string) {
      notification.success({ ...opt, message: WhiteMsg(msg), duration: 1.5, icon: iconSuccess });
    },
    error(msg: string) {
      notification.error({ ...opt, message: WhiteMsg(msg), duration: 3, icon: iconError });
    },
    warning(msg: string) {
      notification.warning({ ...opt, message: WhiteMsg(msg), duration: 3, icon: iconWarning });
    },
    apiError(err: unknown, fallback = '操作失败') {
      const msg = getErrorMessage(err, fallback);
      notification.error({ ...opt, message: WhiteMsg(msg), duration: 3, icon: iconError });
    },
  };
}

/* ---------- helpers ---------- */

function getErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const axiosErr = err as { response?: { data?: { message?: string } }; message?: string };
    return axiosErr.response?.data?.message ?? axiosErr.message ?? fallback;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}
