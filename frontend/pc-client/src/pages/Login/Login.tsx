import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../stores/authStore';
import styles from './Login.module.css';

export function Login() {
  const navigate = useNavigate();
  const login = useAuthStore((s) => s.login);
  const isLoggedIn = useAuthStore((s) => s.isLoggedIn);

  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (isLoggedIn) {
      navigate('/');
    }
  }, [isLoggedIn, navigate]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!userName || !password) {
      setError('请输入用户名和密码');
      return;
    }
    setLoading(true);
    setError('');
    try {
      await login(userName, password);
      navigate('/');
    } catch (err: any) {
      setError(err.message || '登录失败，请检查用户名和密码');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.brand}>
        <h1 className={styles.brandTitle}>漫味</h1>
        <p className={styles.brandSubtitle}>追番情感管理</p>
      </div>
      <div className={styles.formArea}>
        <div className={styles.formHeader}>
          <h2 className={styles.formTitle}>欢迎回来</h2>
          <p className={styles.formDesc}>登录你的账号</p>
        </div>
        <form onSubmit={handleSubmit} className={styles.form}>
          <div className={styles.field}>
            <input
              type="text"
              id="userName"
              value={userName}
              onChange={(e) => setUserName(e.target.value)}
              placeholder="用户名"
              className={styles.input}
              disabled={loading}
            />
          </div>
          <div className={styles.field}>
            <input
              type="password"
              id="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="密码"
              className={styles.input}
              disabled={loading}
            />
          </div>
          {error && <p className={styles.error}>{error}</p>}
          <button type="submit" className={styles.button} disabled={loading}>
            {loading ? '登录中...' : '登录'}
          </button>
          <div className={styles.footer}>
            还没有账号？<a href="/register" className={styles.link}>立即注册</a>
          </div>
        </form>
      </div>
    </div>
  );
}