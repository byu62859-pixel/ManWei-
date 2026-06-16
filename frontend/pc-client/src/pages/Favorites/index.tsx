import { useState, useEffect, useCallback } from 'react';
import { Spin, Pagination, Button } from 'antd';
import { useFavorites } from './hooks/useFavorites';
import { FavoriteCard } from './components/FavoriteCard';
import { StatusTabs } from './components/StatusTabs';
import { FilterBar } from './components/FilterBar';
import { ProgressModal } from './components/ProgressModal';
import { AddFavoriteModal } from './components/AddFavoriteModal';
import { useFavoritesStore } from './store/favoritesStore';
import type { FavoriteStatus } from './types';
import styles from './Favorites.module.css';

export function Favorites() {
  const {
    list,
    loading,
    pagination,
    filter,
    counts,
    fetchFavorites,
    fetchCounts,
    setFilter,
    setPage,
  } = useFavorites();

  const [progressModalVisible, setProgressModalVisible] = useState(false);
  const [selectedFavoriteId, setSelectedFavoriteId] = useState<number | null>(null);
  const [selectedProgress, setSelectedProgress] = useState(0);
  const { addModalVisible, setAddModalVisible } = useFavoritesStore();

  const handleStatusChange = useCallback((status: FavoriteStatus | null) => {
    setFilter({ status });
    setPage(1);
  }, [setFilter, setPage]);

  const handleOrderByChange = useCallback((orderBy: string) => {
    setFilter({ orderBy });
    setPage(1);
  }, [setFilter, setPage]);

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
  };

  const handleProgressClick = (favoriteId: number, currentProgress: number) => {
    setSelectedFavoriteId(favoriteId);
    setSelectedProgress(currentProgress);
    setProgressModalVisible(true);
  };

  useEffect(() => {
    fetchFavorites();
    fetchCounts();
  }, [fetchFavorites, fetchCounts, filter.status, filter.orderBy, pagination.page]);

  return (
    <div className={styles.container}>
      <main className={styles.main}>
        <div className={styles.mainContent}>
          <div className={styles.pageHeader}>
            <h2 className={styles.pageTitle}>我的收藏</h2>
            <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
              <span className={styles.stats}>共 {pagination.total} 部</span>
              <Button type="primary" onClick={() => setAddModalVisible(true)}>
                + 添加收藏
              </Button>
            </div>
          </div>

          <div className={styles.filterBar}>
            <StatusTabs
              activeStatus={filter.status}
              onChange={handleStatusChange}
              counts={counts}
            />
            <FilterBar
              orderBy={filter.orderBy}
              onOrderByChange={handleOrderByChange}
            />
          </div>

          <Spin spinning={loading}>
            {list.length > 0 ? (
              <div className={styles.favoritesGrid}>
                {list.map((favorite) => (
                  <FavoriteCard
                    key={favorite.id}
                    favorite={favorite}
                    onProgressClick={handleProgressClick}
                  />
                ))}
              </div>
            ) : (
              <div className={styles.emptyState}>
                <p>暂无收藏</p>
              </div>
            )}
          </Spin>

          {pagination.total > 0 && (
            <div className={styles.pagination}>
              <Pagination
                current={pagination.page}
                pageSize={pagination.pageSize}
                total={pagination.total}
                onChange={handlePageChange}
                showSizeChanger={false}
                showTotal={(t) => `共 ${t} 部动漫`}
              />
            </div>
          )}
        </div>
      </main>

      {selectedFavoriteId && (() => {
        const targetFavorite = list.find(f => f.id === selectedFavoriteId);
        const maxEpisodes = targetFavorite?.animeTotalEpisodes && targetFavorite.animeTotalEpisodes > 0
          ? targetFavorite.animeTotalEpisodes
          : 500;
        return (
          <ProgressModal
            visible={progressModalVisible}
            favoriteId={selectedFavoriteId}
            currentProgress={selectedProgress}
            maxProgress={maxEpisodes}
            onClose={() => setProgressModalVisible(false)}
          />
        );
      })()}

      <AddFavoriteModal
        visible={addModalVisible}
        onClose={() => setAddModalVisible(false)}
      />
    </div>
  );
}
