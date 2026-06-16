import { App } from 'antd';
import type { ReactNode } from 'react';

const baseStyle: React.CSSProperties = {
  background: '#1C1C1E',
  color: '#fff',
  borderRadius: 6,
  boxShadow: '0 2px 12px rgba(0,0,0,0.15)',
};

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

export function useNotify() {
  const { notification } = App.useApp();

  return {
    success(msg: string) {
      notification.success({
        message: msg,
        placement: 'bottomRight',
        duration: 1.5,
        className: 'dark-toast',
        style: baseStyle,
        icon: iconSuccess,
        closeIcon: null,
      });
    },
    error(msg: string) {
      notification.error({
        message: msg,
        placement: 'bottomRight',
        duration: 3,
        className: 'dark-toast',
        style: baseStyle,
        icon: iconError,
        closeIcon: null,
      });
    },
    warning(msg: string) {
      notification.warning({
        message: msg,
        placement: 'bottomRight',
        duration: 3,
        className: 'dark-toast',
        style: baseStyle,
        icon: iconWarning,
        closeIcon: null,
      });
    },
    /** 从 axios error 中自动提取服务端错误消息 */
    apiError(err: unknown, fallback = '操作失败') {
      const msg = getErrorMessage(err, fallback);
      notification.error({
        message: msg,
        placement: 'bottomRight',
        duration: 3,
        className: 'dark-toast',
        style: baseStyle,
        icon: iconError,
        closeIcon: null,
      });
    },
  };
}

function getErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const axiosErr = err as { response?: { data?: { message?: string } }; message?: string };
    return axiosErr.response?.data?.message ?? axiosErr.message ?? fallback;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}
