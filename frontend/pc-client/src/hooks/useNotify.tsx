import { useToast } from '../components/Toast';

/* ---- dot colors ---- */

const DOT_SUCCESS = '#D4A574';
const DOT_ERROR = '#ef4444';
const DOT_WARNING = '#D4A574';

/* ---- message wrapper ---- */

const msgStyle = { color: '#fff' };
function WhiteMsg(msg: string) {
  return <span style={msgStyle}>{msg}</span>;
}

/* ---- hook ---- */

export function useNotify() {
  const toast = useToast();

  return {
    success(msg: string) {
      toast.push(WhiteMsg(msg), DOT_SUCCESS, 1500);
    },
    error(msg: string) {
      toast.push(WhiteMsg(msg), DOT_ERROR, 3000);
    },
    warning(msg: string) {
      toast.push(WhiteMsg(msg), DOT_WARNING, 3000);
    },
    apiError(err: unknown, fallback = '操作失败') {
      const msg = getErrorMessage(err, fallback);
      toast.push(WhiteMsg(msg), DOT_ERROR, 3000);
    },
  };
}

/* ---- helpers ---- */

function getErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const axiosErr = err as { response?: { data?: { message?: string } }; message?: string };
    return axiosErr.response?.data?.message ?? axiosErr.message ?? fallback;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}
