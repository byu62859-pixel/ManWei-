import { Modal, InputNumber, Slider, Button, Space } from 'antd';
import { useNotify } from '../../../hooks/useNotify';
import { useState } from 'react';
import { useFavoritesStore } from '../store/favoritesStore';

interface ProgressModalProps {
  visible: boolean;
  favoriteId: number;
  currentProgress: number;
  maxProgress?: number;
  onClose: () => void;
}

export function ProgressModal({ visible, favoriteId, currentProgress, maxProgress = 500, onClose }: ProgressModalProps) {
  const [progress, setProgress] = useState(currentProgress);
  const { updateProgress } = useFavoritesStore();
  const notify = useNotify();

  const handleSave = async () => {
    if (progress < 0) {
      notify.warning('进度不能为负数');
      return;
    }
    try {
      await updateProgress(favoriteId, progress);
      notify.success('已更新');
      onClose();
    } catch (err) {
      notify.apiError(err, '更新失败');
    }
  };

  return (
    <Modal
      title="更新进度"
      open={visible}
      onCancel={onClose}
      footer={null}
    >
      <div style={{ padding: '16px 0' }}>
        <div style={{ marginBottom: 16 }}>
          <label style={{ display: 'block', marginBottom: 8, fontSize: 14 }}>
            集数
          </label>
          <InputNumber
            min={0}
            max={maxProgress}
            value={progress}
            onChange={(value) => setProgress(value || 0)}
            style={{ width: '100%' }}
          />
        </div>
        <div style={{ marginBottom: 16 }}>
          <label style={{ display: 'block', marginBottom: 8, fontSize: 14 }}>
            或滑动选择
          </label>
          <Slider
            min={0}
            max={maxProgress}
            value={progress}
            onChange={setProgress}
          />
        <div style={{ marginTop: 8, fontSize: 12, color: '#6B6B6B' }}>
          最多 {maxProgress} 集
        </div>
        </div>
        <div style={{ textAlign: 'right', marginTop: 24 }}>
          <Space>
            <Button onClick={onClose}>取消</Button>
            <Button type="primary" onClick={handleSave}>
              保存
            </Button>
          </Space>
        </div>
      </div>
    </Modal>
  );
}