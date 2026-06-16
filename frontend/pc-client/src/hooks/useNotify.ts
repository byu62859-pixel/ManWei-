import { App } from 'antd';

export function useNotify() {
  const { notification } = App.useApp();

  return {
    success(msg: string) {
      notification.success({ message: msg, placement: 'bottomRight', duration: 1.5 });
    },
    error(msg: string) {
      notification.error({ message: msg, placement: 'bottomRight', duration: 3 });
    },
    warning(msg: string) {
      notification.warning({ message: msg, placement: 'bottomRight', duration: 3 });
    },
    /** 从 axios error 中自动提取服务端错误消息 */
    apiError(err: unknown, fallback = '操作失败') {
      const msg = getErrorMessage(err, fallback);
      notification.error({ message: msg, placement: 'bottomRight', duration: 3 });
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
