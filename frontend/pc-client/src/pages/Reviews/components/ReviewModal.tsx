import { Modal, Button, Space, Spin } from 'antd';
import { useEffect, useState } from 'react';
import { useReviewsStore } from '../store/reviewsStore';
import type { ReviewDto } from '../types';
import request from '../../../services/request';
import { useNotify } from '../../../hooks/useNotify';
import MDEditor from '@uiw/react-md-editor';

interface ReviewModalProps {
  visible: boolean;
  review: ReviewDto | null;
  favoriteId: number;  // 新增
  onClose: () => void;
  onSaved?: () => void;
}

export function ReviewModal({ visible, review, favoriteId, onClose, onSaved }: ReviewModalProps) {
  const notify = useNotify();
  const { saveReview } = useReviewsStore();
  const [loading, setLoading] = useState(false);
  const [content, setContent] = useState('');

  useEffect(() => {
    if (!visible) {
      setContent('');
      return;
    }
    if (favoriteId) {
      setContent('');  // 先清空防闪旧内容
      setLoading(true);
      request.get(`/reviews/${favoriteId}`).then((res: any) => {
        if (res.code === 200 && res.data) {
          setContent(res.data.content ?? '');
        }
      }).finally(() => setLoading(false));
    }
  }, [visible, favoriteId]);

  const handleSave = async () => {
    if (!content.trim()) {
      notify.warning('内容不能为空');
      return;
    }
    await saveReview(favoriteId, content);
    notify.success('已保存');
    onSaved?.();
    onClose();
  };

  return (
    <Modal
      title={review ? '编辑观后感' : '撰写观后感'}
      open={visible}
      onCancel={onClose}
      footer={null}
      width={600}
      destroyOnHidden
    >
      <Spin spinning={loading}>
        {review && (
          <div style={{ marginBottom: 16, color: '#6B6B6B' }}>
            动漫: {review.animeName}
          </div>
        )}
        <div data-color-mode="light">
          <MDEditor
            key={visible ? 'open' : 'closed'}
            value={content}
            onChange={(val) => setContent(val ?? '')}
            data-color-mode="light"
            height={200}
          />
        </div>
        <div style={{ marginTop: 16 }}>
          <Space style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <Button onClick={onClose}>取消</Button>
            <Button type="primary" onClick={handleSave}>
              保存
            </Button>
          </Space>
        </div>
      </Spin>
    </Modal>
  );
}