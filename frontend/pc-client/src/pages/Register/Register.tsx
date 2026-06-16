import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import request from '../../services/request';
import styles from './Register.module.css';

export function Register() {
  const navigate = useNavigate();

  const [userName, setUserName] = useState('');
  const [nickName, setNickName] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (!userName || !password) {
      setError('请输入用户名和密码');
      return;
    }

    if (password !== confirmPassword) {
      setError('两次密码输入不一致');
      return;
    }

    if (password.length < 6) {
      setError('密码长度不能少于6位');
      return;
    }

    setLoading(true);
    try {
      const res: any = await request.post('/auth/Register', {
        Username: userName,
        Password: password,
        NickName: nickName || undefined,
      });
      if (res.code === 200) {
        navigate('/login');
      } else {
        setError(res.message || '注册失败，请稍后重试');
      }
    } catch (err: any) {
      setError(err.response?.data?.message || '注册失败，请稍后重试');
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
          <h2 className={styles.formTitle}>创建账号</h2>
          <p className={styles.formDesc}>开始你的追番之旅</p>
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
              type="text"
              id="nickName"
              value={nickName}
              onChange={(e) => setNickName(e.target.value)}
              placeholder="昵称（可选）"
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
          <div className={styles.field}>
            <input
              type="password"
              id="confirmPassword"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              placeholder="确认密码"
              className={styles.input}
              disabled={loading}
            />
          </div>
          {error && <p className={styles.error}>{error}</p>}
          <button type="submit" className={styles.button} disabled={loading}>
            {loading ? '注册中...' : '注册'}
          </button>
          <div className={styles.footer}>
            已有账号？<a href="/login" className={styles.link}>立即登录</a>
          </div>
        </form>
      </div>
    </div>
  );
}