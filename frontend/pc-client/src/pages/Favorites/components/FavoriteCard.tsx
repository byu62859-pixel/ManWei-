import { Card, Rate, Popconfirm, message } from 'antd';
import { CloseCircleOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import type { FavoriteDto } from '../types';
import { STATUS_LABELS } from '../types';
import { useFavoritesStore } from '../store/favoritesStore';
import styles from './FavoriteCard.module.css';

interface FavoriteCardProps {
  favorite: FavoriteDto;
  onProgressClick: (favoriteId: number, currentProgress: number) => void;
}

export function FavoriteCard({ favorite, onProgressClick }: FavoriteCardProps) {
  const navigate = useNavigate();
  const { deleteFavorite } = useFavoritesStore();

  const handleCardClick = () => {
    navigate(`/anime/${favorite.animeId}`);
  };

  const handleProgressClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    onProgressClick(favorite.id, favorite.progress);
  };

  const handleDeleteAreaClick = (e: React.MouseEvent) => {
    e.stopPropagation();
  };

  const handleDeleteConfirm = (e?: React.MouseEvent) => {
    e?.stopPropagation();
    deleteFavorite(favorite.id);
    message.success('已删除');
  };

  const handleDeleteCancel = (e?: React.MouseEvent) => {
    e?.stopPropagation();
  };

  const statusLabel = STATUS_LABELS[favorite.status];

  return (
    <Card
      className={styles.card}
      cover={
        <div className={styles.coverWrapper}>
          {favorite.animeCover ? (
            <img
              src={favorite.animeCover}
              alt={favorite.animeName}
              className={styles.cover}
            />
          ) : (
            <div className={styles.coverPlaceholder}>
              <span>暂无封面</span>
            </div>
          )}
          <Popconfirm
            title="确定删除收藏？"
            onConfirm={handleDeleteConfirm}
            onCancel={handleDeleteCancel}
            okText="确定"
            cancelText="取消"
          >
            <button
              className={styles.deleteBtn}
              onClick={(e) => e.stopPropagation()}
            >
              <CloseCircleOutlined style={{ fontSize: 16 }} />
            </button>
          </Popconfirm>
        </div>
      }
      onClick={handleCardClick}
      hoverable
    >
      <div className={styles.content}>
        <div className={styles.titleRow}>
          <span className={styles.title}>{favorite.animeName}</span>
          <span className={`${styles.statusBadge} ${styles[`status${favorite.status}`]}`}>
            {statusLabel}
          </span>
        </div>

        <div className={styles.meta}>
          <span className={styles.type}>{favorite.animeType}</span>
        </div>

        {favorite.status === 1 && (
          <div className={styles.progress}>
            <span>
              进度: {favorite.progress} / {favorite.animeTotalEpisodes && favorite.animeTotalEpisodes > 0 ? favorite.animeTotalEpisodes : '?'} 集
            </span>
          </div>
        )}

        {favorite.rating && (
          <div className={styles.rating}>
            <Rate
              disabled
              value={Math.round(favorite.rating / 2)}
              style={{ fontSize: 12 }}
            />
          </div>
        )}

        <div
          className={styles.actions}
          onClick={handleDeleteAreaClick}
        >
          <button
            className={styles.actionBtn}
            onClick={handleProgressClick}
          >
            更新进度
          </button>
        </div>
      </div>
    </Card>
  );
}