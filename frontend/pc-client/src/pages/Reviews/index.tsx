import { useState, useEffect } from 'react';
import { Spin, Pagination, Select } from 'antd';
import { useReviews } from './hooks/useReviews';
import { ReviewCard } from './components/ReviewCard';
import { ReviewModal } from './components/ReviewModal';
import type { ReviewDto } from './types';
import styles from './Reviews.module.css';

export function Reviews() {
  const {
    list,
    loading,
    pagination,
    orderBy,
    fetchReviews,
    setPage,
    setOrderBy,
  } = useReviews();

  const [modalVisible, setModalVisible] = useState(false);
  const [selectedReview, setSelectedReview] = useState<ReviewDto | null>(null);

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
  };

  const handleOrderByChange = (value: string) => {
    setOrderBy(value);
    setPage(1);
  };

  const handleEdit = (review: ReviewDto) => {
    setSelectedReview(review);
    setModalVisible(true);
  };

  const handleModalClose = () => {
    setModalVisible(false);
    setSelectedReview(null);
  };

  useEffect(() => {
    fetchReviews();
  }, [fetchReviews, pagination.page, orderBy]);

  return (
    <div className={styles.container}>
      <main className={styles.main}>
        <div className={styles.mainContent}>
          <div className={styles.pageHeader}>
            <h2 className={styles.pageTitle}>我的观后感</h2>
            <span className={styles.stats}>共 {pagination.total} 篇</span>
          </div>

          <div className={styles.filterBar}>
            <Select
              value={orderBy}
              onChange={handleOrderByChange}
              style={{ width: 160 }}
              options={[
                { value: 'CreateTime', label: '按时间排序' },
                { value: 'AnimeName', label: '按动漫名称' },
              ]}
            />
          </div>

          <Spin spinning={loading}>
            {list.length > 0 ? (
              <div className={styles.reviewsList}>
                {list.map((review) => (
                  <ReviewCard
                    key={review.favoriteId}
                    review={review}
                    onEdit={handleEdit}
                  />
                ))}
              </div>
            ) : (
              <div className={styles.emptyState}>
                <p>暂无观后感</p>
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
                showTotal={(t) => `共 ${t} 篇观后感`}
              />
            </div>
          )}
        </div>
      </main>

      <ReviewModal
        visible={modalVisible}
        review={selectedReview}
        favoriteId={selectedReview?.favoriteId ?? 0}
        onClose={handleModalClose}
      />
    </div>
  );
}
