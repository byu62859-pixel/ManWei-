import { useEffect } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { Button, Modal } from 'antd';
import { AiAssistantIcon } from '../AiAssistantIcon';
import { useAuthStore } from '../../stores/authStore';
import { useAiAssistantStore } from '../../stores/aiAssistantStore';
import { AiAssistantDrawer } from '../AiAssistantDrawer';
import styles from './AppShell.module.css';

const getInitial = (name?: string | null) => {
  const trimmed = name?.trim();
  return trimmed ? trimmed.slice(0, 1).toUpperCase() : '用';
};

export function AppShell() {
  const navigate = useNavigate();
  const openDrawer = useAiAssistantStore(s => s.openDrawer);
  const { userInfo, fetchMe, logout } = useAuthStore();

  useEffect(() => {
    if (!userInfo) {
      fetchMe().catch(() => {
        logout();
        navigate('/login');
      });
    }
  }, [fetchMe, logout, navigate, userInfo]);

  const handleLogout = () => {
    Modal.confirm({
      title: '确认退出登录？',
      content: '退出后需要重新登录才能继续管理追番记录。',
      okText: '退出',
      cancelText: '取消',
      okButtonProps: { danger: true },
      onOk: () => {
        logout();
        navigate('/login');
      },
    });
  };

  const navClassName = ({ isActive }: { isActive: boolean }) =>
    isActive ? `${styles.navItem} ${styles.navItemActive}` : styles.navItem;

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <div className={styles.headerContent}>
          <NavLink to="/" className={styles.brand}>
            <h1 className={styles.brandTitle}>漫味</h1>
          </NavLink>

          <nav className={styles.nav} aria-label="主导航">
            <NavLink to="/" end className={navClassName}>首页</NavLink>
            <NavLink to="/favorites" className={navClassName}>收藏</NavLink>
            <NavLink to="/reviews" className={navClassName}>观后感</NavLink>
            <NavLink to="/data" className={navClassName}>数据中心</NavLink>
            <NavLink to="/profile" className={navClassName}>个人中心</NavLink>
          </nav>

          <div className={styles.userArea}>
            <NavLink to="/profile" className={styles.profileLink} aria-label="进入个人中心">
              {userInfo?.avatar ? (
                <img className={styles.avatar} src={userInfo.avatar} alt={`${userInfo.nickName || '用户'}头像`} />
              ) : (
                <span className={styles.avatarFallback}>{getInitial(userInfo?.nickName)}</span>
              )}
              <span className={styles.userName}>{userInfo?.nickName || '用户'}</span>
            </NavLink>
            <Button
              type="text"
              icon={<AiAssistantIcon />}
              onClick={openDrawer}
              className={styles.aiButton}
            >
              AI 助手
            </Button>
            <button className={styles.logoutBtn} type="button" onClick={handleLogout}>退出</button>
          </div>
        </div>
      </header>

      <Outlet />
      <AiAssistantDrawer />
    </div>
  );
}
