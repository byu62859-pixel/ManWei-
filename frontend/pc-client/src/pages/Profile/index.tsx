import { useEffect, useState } from 'react';
import { Button, Form, Input, Spin, Upload } from 'antd';
import type { UploadProps } from 'antd';
import { useAuthStore } from '../../stores/authStore';
import { useNotify } from '../../hooks/useNotify';
import { getMe, getMyStats, updateNickname, uploadAvatar } from '../../services/user';
import type { UserProfile, UserStats } from '../../types/api';
import styles from './Profile.module.css';

const MAX_AVATAR_SIZE = 2 * 1024 * 1024;
const AVATAR_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

const getInitial = (name?: string | null) => {
  const trimmed = name?.trim();
  return trimmed ? trimmed.slice(0, 1).toUpperCase() : '用';
};

const formatDate = (value?: string) => {
  if (!value) return '-';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '-' : date.toLocaleDateString('zh-CN');
};

const roleLabel = (role?: string) => (role === 'Admin' ? '管理员' : '普通用户');

export function Profile() {
  const notify = useNotify();
  const [form] = Form.useForm<{ nickName: string }>();
  const { userInfo, updateUserInfo } = useAuthStore();
  const [profile, setProfile] = useState<UserProfile | null>(userInfo);
  const [stats, setStats] = useState<UserStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);

  useEffect(() => {
    let ignore = false;

    const loadProfile = async () => {
      setLoading(true);
      try {
        const [me, userStats] = await Promise.all([getMe(), getMyStats()]);
        if (ignore) return;

        setProfile(me);
        setStats(userStats);
        updateUserInfo(me);
        form.setFieldsValue({ nickName: me.nickName || '' });
      } catch (err) {
        notify.apiError(err, '获取资料失败');
      } finally {
        if (!ignore) setLoading(false);
      }
    };

    loadProfile();

    return () => {
      ignore = true;
    };
  }, [form, updateUserInfo]);

  const handleSaveNickname = async ({ nickName }: { nickName: string }) => {
    const trimmed = nickName.trim();
    if (!trimmed) {
      notify.warning('昵称不能为空');
      return;
    }

    setSaving(true);
    try {
      const nextProfile = await updateNickname(trimmed);
      setProfile(nextProfile);
      updateUserInfo(nextProfile);
      form.setFieldsValue({ nickName: nextProfile.nickName || '' });
      notify.success('已更新');
    } catch (err) {
      notify.apiError(err, '更新失败');
    } finally {
      setSaving(false);
    }
  };

  const handleAvatarUpload = async (file: File) => {
    if (!AVATAR_TYPES.includes(file.type)) {
      notify.warning('仅支持 JPG、PNG、WEBP 图片');
      return;
    }

    if (file.size > MAX_AVATAR_SIZE) {
      notify.warning('头像不能超过2MB');
      return;
    }

    setUploading(true);
    try {
      const nextProfile = await uploadAvatar(file);
      setProfile(nextProfile);
      updateUserInfo(nextProfile);
      notify.success('已上传');
    } catch (err) {
      notify.apiError(err, '上传失败');
    } finally {
      setUploading(false);
    }
  };

  const uploadProps: UploadProps = {
    accept: 'image/jpeg,image/png,image/webp',
    showUploadList: false,
    beforeUpload: (file) => {
      handleAvatarUpload(file);
      return Upload.LIST_IGNORE;
    },
  };

  if (loading) {
    return (
      <div className={styles.container}>
        <main className={styles.main}>
          <div className={styles.loading}>
            <Spin size="large" />
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <main className={styles.main}>
        <div className={styles.mainContent}>
          <div className={styles.pageHeader}>
            <h2 className={styles.pageTitle}>个人中心</h2>
            <p className={styles.pageSubtitle}>管理你的资料，并快速查看追番记录概览</p>
          </div>

          <div className={styles.contentGrid}>
            <section className={`${styles.panel} ${styles.profilePanel}`}>
              <div className={styles.avatarFrame}>
                {profile?.avatar ? (
                  <img className={styles.avatarImage} src={profile.avatar} alt={`${profile.nickName || '用户'}头像`} />
                ) : (
                  <span className={styles.avatarFallback}>{getInitial(profile?.nickName)}</span>
                )}
              </div>

              <h3 className={styles.displayName}>{profile?.nickName || '用户'}</h3>
              <p className={styles.roleText}>{roleLabel(profile?.role)}</p>

              <Upload {...uploadProps}>
                <Button loading={uploading}>上传头像</Button>
              </Upload>
              <p className={styles.uploadHint}>支持 JPG、PNG、WEBP，最大 2MB</p>

              <div className={styles.infoList}>
                <div className={styles.infoRow}>
                  <span className={styles.infoLabel}>用户 ID</span>
                  <span className={styles.infoValue}>{profile?.id ?? '-'}</span>
                </div>
                <div className={styles.infoRow}>
                  <span className={styles.infoLabel}>登录账号</span>
                  <span className={styles.infoValue}>{profile?.openId || '-'}</span>
                </div>
                <div className={styles.infoRow}>
                  <span className={styles.infoLabel}>注册时间</span>
                  <span className={styles.infoValue}>{formatDate(profile?.createTime)}</span>
                </div>
              </div>
            </section>

            <div className={styles.stack}>
              <section className={styles.panel}>
                <h3 className={styles.sectionTitle}>资料设置</h3>
                <Form
                  form={form}
                  layout="vertical"
                  className={styles.nicknameForm}
                  onFinish={handleSaveNickname}
                  initialValues={{ nickName: profile?.nickName || '' }}
                >
                  <Form.Item
                    name="nickName"
                    label="昵称"
                    rules={[
                      { required: true, whitespace: true, message: '请输入昵称' },
                      { max: 50, message: '昵称不能超过50个字符' },
                    ]}
                  >
                    <Input placeholder="输入昵称" maxLength={50} />
                  </Form.Item>
                  <Button type="primary" htmlType="submit" loading={saving}>
                    保存昵称
                  </Button>
                </Form>
              </section>

              <section className={styles.panel}>
                <h3 className={styles.sectionTitle}>追番统计</h3>
                <div className={styles.statsGrid}>
                  <div className={styles.statItem}>
                    <span className={styles.statLabel}>总集数</span>
                    <strong className={styles.statValue}>{stats?.totalEpisodes ?? 0}</strong>
                    <span className={styles.statHint}>观看过的总集数</span>
                  </div>
                  <div className={styles.statItem}>
                    <span className={styles.statLabel}>平均评分</span>
                    <strong className={styles.statValue}>{stats?.avgRating?.toFixed(1) ?? '-'}</strong>
                    <span className={styles.statHint}>综合评分 1-10</span>
                  </div>
                  <div className={styles.statItem}>
                    <span className={styles.statLabel}>观后感</span>
                    <strong className={styles.statValue}>{stats?.reviewCount ?? 0}</strong>
                    <span className={styles.statHint}>撰写数量</span>
                  </div>
                </div>
              </section>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
