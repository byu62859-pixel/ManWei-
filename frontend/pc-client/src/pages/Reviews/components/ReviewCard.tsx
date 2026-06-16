import { useState } from 'react';
import { Card, Button, Modal, message } from 'antd';
import { useNavigate } from 'react-router-dom';
import type { ReviewDto } from '../types';
import { useReviewsStore } from '../store/reviewsStore';
import styles from './ReviewCard.module.css';

interface ReviewCardProps {
  review: ReviewDto;
  onEdit: (review: ReviewDto) => void;
}

export function ReviewCard({ review, onEdit }: ReviewCardProps) {
  const navigate = useNavigate();
  const { deleteReview } = useReviewsStore();
  const [deleteModalVisible, setDeleteModalVisible] = useState(false);

  const handleCardClick = () => {
    navigate(`/anime/${review.animeId}`);
  };

  const handleDelete = () => {
    deleteReview(review.favoriteId);
  };

  const formatDate = (dateStr: string) => {
    if (!dateStr) return '';
    const date = new Date(dateStr);
    if (isNaN(date.getTime())) return '';
    return date.toLocaleDateString('zh-CN');
  };

  return (
    <Card className={styles.card} hoverable onClick={handleCardClick}>
      <div className={styles.header}>
        {review.animeCover && (
          <img
            src={review.animeCover}
            alt={review.animeName}
            className={styles.cover}
          />
        )}
        <div className={styles.animeInfo}>
          <h3 className={styles.animeName}>{review.animeName}</h3>
        </div>
      </div>

      <div className={styles.content}>
        <p className={styles.reviewText}>{review.contentSummary || review.content}</p>
      </div>

      <div className={styles.footer}>
        <span className={styles.date}>
          {formatDate(review.createTime)}
          {review.updatedAt !== review.createTime && ` · 已编辑`}
        </span>
        <div className={styles.actions}>
          <Button
            size="small"
            onClick={(e) => {
              e.stopPropagation();
              onEdit(review);
            }}
          >
            编辑
          </Button>
          <Button
            size="small"
            danger
            onClick={(e) => {
              e.stopPropagation();
              setDeleteModalVisible(true);
            }}
          >
            删除
          </Button>
        </div>
      </div>

      <Modal
        title="确定删除这篇观后感？"
        open={deleteModalVisible}
        onCancel={(e) => {
          e.stopPropagation();
          setDeleteModalVisible(false);
        }}
        onOk={(e) => {
          e.stopPropagation();
          handleDelete();
          setDeleteModalVisible(false);
          message.success('已删除');
        }}
        okText="确定"
        cancelText="取消"
        okButtonProps={{ danger: true }}
      />
    </Card>
  );
}