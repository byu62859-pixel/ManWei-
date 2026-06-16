import { useState, useEffect, useRef } from 'react';
import { Modal, Input, Button, Space, Spin } from 'antd';
import { useNotify } from '../../../hooks/useNotify';
import { debounce } from 'lodash';
import { useFavoritesStore } from '../store/favoritesStore';

interface AddFavoriteModalProps {
  visible: boolean;
  onClose: () => void;
}

export function AddFavoriteModal({ visible, onClose }: AddFavoriteModalProps) {
  const {
    searchResults,
    searchLoading,
    selectedItem,
    searchAnime,
    addFavorite,
    resetAddModal,
    setSelectedItem,
  } = useFavoritesStore();

  const [keyword, setKeyword] = useState('');
  const [adding, setAdding] = useState(false);
  const notify = useNotify();

  const searchAnimeRef = useRef(searchAnime);
  useEffect(() => {
    searchAnimeRef.current = searchAnime;
  }, [searchAnime]);

  const debouncedSearch = useRef(
    debounce((kw: string) => {
      searchAnimeRef.current(kw);
    }, 300)
  ).current;

  useEffect(() => () => debouncedSearch.cancel(), [debouncedSearch]);

  const handleKeywordChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value;
    setKeyword(value);
    setSelectedItem(null);
    debouncedSearch(value);
  };

  const handleAdd = async () => {
    if (!selectedItem) return;
    setAdding(true);
    const params = selectedItem.animeId
      ? { animeId: selectedItem.animeId }
      : { bangumiId: selectedItem.bangumiId };
    const result = await addFavorite(params);
    setAdding(false);
    if (result.code === 200) {
      notify.success('已收藏');
      handleClose();
    } else if (result.code === 409) {
      notify.warning('已收藏过');
    } else if (result.code === 503) {
      notify.error('服务繁忙，请稍后重试');
    } else {
      notify.error('添加失败');
    }
  };

  const handleClose = () => {
    setKeyword('');
    resetAddModal();
    onClose();
  };

  return (
    <Modal
      title="添加收藏"
      open={visible}
      onCancel={handleClose}
      footer={null}
      width={520}
    >
      <div style={{ padding: '8px 0' }}>
        <Input
          placeholder="输入动漫名称搜索..."
          value={keyword}
          onChange={handleKeywordChange}
          style={{ marginBottom: 16 }}
          allowClear
          onClear={() => {
            setKeyword('');
            setSelectedItem(null);
            searchAnime('');
          }}
        />

        <Spin spinning={searchLoading}>
          <div style={{ maxHeight: 360, overflowY: 'auto' }}>
            {searchResults.length > 0 ? (
              searchResults.map((item) => (
                <div
                  key={`${item.source}-${item.bangumiId}`}
                  onClick={() =>
                    setSelectedItem(
                      selectedItem?.bangumiId === item.bangumiId ? null : item
                    )
                  }
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 12,
                    padding: '10px 12px',
                    marginBottom: 4,
                    border: '1px solid',
                    borderColor:
                      selectedItem?.bangumiId === item.bangumiId
                        ? '#1C1C1E'
                        : '#E8E4DE',
                    cursor: 'pointer',
                    background:
                      selectedItem?.bangumiId === item.bangumiId
                        ? '#F5F5F5'
                        : '#ffffff',
                    transition: 'all 0.15s',
                  }}
                >
                  {item.cover ? (
                    <img
                      src={item.cover}
                      alt={item.name}
                      style={{
                        width: 40,
                        height: 56,
                        objectFit: 'cover',
                        flexShrink: 0,
                      }}
                    />
                  ) : (
                    <div
                      style={{
                        width: 40,
                        height: 56,
                        background: '#E8E4DE',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        fontSize: 11,
                        color: '#6B6B6B',
                        flexShrink: 0,
                      }}
                    >
                      无图
                    </div>
                  )}

                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div
                      style={{
                        fontSize: 14,
                        fontWeight: 500,
                        color: '#1C1C1E',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        whiteSpace: 'nowrap',
                      }}
                    >
                      {item.name}
                    </div>
                    <div style={{ fontSize: 12, color: '#6B6B6B', marginTop: 2 }}>
                      {item.animeType} · ID: {item.bangumiId}
                    </div>
                  </div>

                  <span
                    style={{
                      fontSize: 11,
                      padding: '2px 6px',
                      background:
                        item.source === 'local' ? '#E8F5E9' : '#FFF7E6',
                      color:
                        item.source === 'local' ? '#4CAF50' : '#D4A574',
                      flexShrink: 0,
                    }}
                  >
                    {item.source === 'local' ? '本地' : 'Bangumi'}
                  </span>
                </div>
              ))
            ) : keyword && !searchLoading ? (
              <div
                style={{
                  textAlign: 'center',
                  padding: '32px 0',
                  color: '#6B6B6B',
                  fontSize: 14,
                }}
              >
                未找到相关动漫
              </div>
            ) : !keyword ? (
              <div
                style={{
                  textAlign: 'center',
                  padding: '32px 0',
                  color: '#6B6B6B',
                  fontSize: 14,
                }}
              >
                输入动漫名称开始搜索
              </div>
            ) : null}
          </div>
        </Spin>

        <div
          style={{
            textAlign: 'right',
            marginTop: 16,
            paddingTop: 16,
            borderTop: '1px solid #E8E4DE',
          }}
        >
          <Space>
            <Button onClick={handleClose}>取消</Button>
            <Button
              type="primary"
              onClick={handleAdd}
              disabled={!selectedItem}
              loading={adding}
            >
              添加到收藏
            </Button>
          </Space>
        </div>
      </div>
    </Modal>
  );
}