import { useState, useEffect, useCallback } from 'react';
import { useParams } from 'react-router-dom';
import { Breadcrumb, Spin, message, Button, Select, InputNumber, Rate, Tabs, Space, Modal, Form, Input, Popconfirm, Tag } from 'antd';
import ReactECharts from 'echarts-for-react';
import { ReviewModal } from '../Reviews/components/ReviewModal';
import ReactMarkdown from 'react-markdown';
import request from '../../services/request';
import type { Anime, EmotionCurve, EmotionTagDto } from '../../types/api';
import type { ReviewDto } from '../Reviews/types';
import styles from './AnimeDetail.module.css';

interface FavoriteCheck {
  isFavorited: boolean;
  favoriteId?: number;
  status?: number;
  rating?: number;
  progress?: number;
}

const STATUS_OPTIONS = [
  { value: 0, label: '想看' },
  { value: 1, label: '在看' },
  { value: 2, label: '看过' },
];

const EMOTION_LEVELS = [
  { value: 1, label: '1 - 平平淡淡' },
  { value: 2, label: '2 - 略微波动' },
  { value: 3, label: '3 - 心潮澎湃' },
  { value: 4, label: '4 - 强烈共鸣' },
  { value: 5, label: '5 - 难以自拔' },
];

// 预置情感标签
const PRESET_TAGS = [
  { id: 0, name: '泪崩', isPreset: true, animeId: 0, createTime: '' },
  { id: 0, name: '热血', isPreset: true, animeId: 0, createTime: '' },
  { id: 0, name: '治愈', isPreset: true, animeId: 0, createTime: '' },
  { id: 0, name: '致郁', isPreset: true, animeId: 0, createTime: '' },
  { id: 0, name: '笑死', isPreset: true, animeId: 0, createTime: '' },
  { id: 0, name: '神作', isPreset: true, animeId: 0, createTime: '' },
];

export function AnimeDetail() {
  const { id } = useParams<{ id: string }>();
  const [anime, setAnime] = useState<Anime | null>(null);
  const [loading, setLoading] = useState(false);
  const [favoriteLoading, setFavoriteLoading] = useState(false);
  const [favoriteCheck, setFavoriteCheck] = useState<FavoriteCheck | null>(null);
  const [emotionCurves, setEmotionCurves] = useState<EmotionCurve[]>([]);
  const [review, setReview] = useState<ReviewDto | null>(null);
  const [emotionModalVisible, setEmotionModalVisible] = useState(false);
  const [reviewModalVisible, setReviewModalVisible] = useState(false);
  const [emotionForm] = Form.useForm();
  const [emotionTags, setEmotionTags] = useState<EmotionTagDto[]>([]);
  const [newTagName, setNewTagName] = useState('');

  const loadAnimeDetail = async () => {
    if (!id) return;
    setLoading(true);
    try {
      const res = await request.get(`/anime/${id}`) as any;
      if (res.code === 200) {
        setAnime(res.data);
      } else {
        message.error('获取动漫详情失败');
      }
    } catch {
      message.error('获取动漫详情失败');
    } finally {
      setLoading(false);
    }
  };

  const checkFavorite = async () => {
    if (!id) return;
    try {
      const res = await request.get(`/favorites/check/${id}`) as any;
      if (res.code === 200) {
        setFavoriteCheck(res.data);
      }
    } catch {
      // 忽略错误
    }
  };

  const loadEmotionCurves = useCallback(async () => {
    if (!favoriteCheck?.favoriteId) return;
    try {
      const res = await request.get(`/emotioncurves/${favoriteCheck.favoriteId}`) as any;
      if (res.code === 200) {
        setEmotionCurves(res.data || []);
      }
    } catch {
      // 忽略错误
    }
  }, [favoriteCheck?.favoriteId]);

  const loadReview = useCallback(async () => {
    if (!favoriteCheck?.favoriteId) return;
    try {
      const res = await request.get(`/reviews/${favoriteCheck.favoriteId}`) as any;
      if (res.code === 200) {
        setReview(res.data ? {
          reviewId: res.data.id,
          favoriteId: res.data.favoriteId,
          animeId: Number(id),
          animeName: anime?.name ?? '',
          animeCover: anime?.cover ?? null,
          content: res.data.content,
          contentSummary: '',
          createTime: res.data.createTime,
          updatedAt: res.data.updateTime,
        } : null);
      }
    } catch {
      // 忽略错误
    }
  }, [favoriteCheck?.favoriteId, id, anime?.name, anime?.cover]);

  const loadEmotionTags = useCallback(async () => {
    if (!id) return;
    try {
      const res = await request.get('/emotiontags', { params: { animeId: id } }) as any;
      if (res.code === 200) {
        const customTags = res.data.filter((t: EmotionTagDto) => !t.isPreset);
        const presetTags = PRESET_TAGS.map(t => ({ ...t, animeId: Number(id) }));
        setEmotionTags([...presetTags, ...customTags]);
      }
    } catch {
      setEmotionTags(PRESET_TAGS.map(t => ({ ...t, animeId: Number(id) })));
    }
  }, [id]);

  const handleAddTag = async () => {
    if (!id || !favoriteCheck?.isFavorited) return;
    if (!newTagName.trim()) {
      message.warning('请输入标签名');
      return;
    }
    try {
      const res = await request.post('/emotiontags', { name: newTagName.trim(), animeId: Number(id) }) as any;
      if (res.code === 200) {
        message.success('添加成功');
        setNewTagName('');
        loadEmotionTags();
      }
    } catch {
      message.error('添加失败');
    }
  };

  const handleDeleteTag = async (tagId: number) => {
    try {
      const res = await request.delete(`/emotiontags/${tagId}`) as any;
      if (res.code === 200) {
        message.success('已删除');
        loadEmotionTags();
      }
    } catch {
      message.error('删除失败');
    }
  };

  useEffect(() => {
    loadAnimeDetail();
    checkFavorite();
  }, [id]);

  useEffect(() => {
    if (favoriteCheck?.isFavorited && favoriteCheck?.favoriteId) {
      loadEmotionCurves();
      loadReview();
      loadEmotionTags();
    }
  }, [favoriteCheck?.isFavorited, favoriteCheck?.favoriteId, loadEmotionCurves, loadReview, loadEmotionTags]);

  const handleToggleFavorite = async () => {
    if (!anime || !favoriteCheck) return;
    setFavoriteLoading(true);
    try {
      if (favoriteCheck.isFavorited && favoriteCheck.favoriteId) {
        await request.delete(`/favorites/${favoriteCheck.favoriteId}`);
        setFavoriteCheck({ isFavorited: false });
        setEmotionCurves([]);
        setReview(null);
        message.success('已取消追番');
      } else {
        const res = await request.post('/favorites', { animeId: anime.id }) as any;
        if (res.code === 200) {
          setFavoriteCheck({
            isFavorited: true,
            favoriteId: res.data.id,
            status: res.data.status,
            rating: res.data.rating,
            progress: res.data.progress,
          });
          message.success('已添加追番');
        }
      }
    } catch {
      message.error('操作失败');
    } finally {
      setFavoriteLoading(false);
    }
  };

  const handleUpdateFavorite = async (updates: { status?: number; progress?: number; rating?: number | null }) => {
    if (!favoriteCheck?.favoriteId) return;
    try {
      const res = await request.put(`/favorites/${favoriteCheck.favoriteId}`, updates) as any;
      if (res.code === 200) {
        setFavoriteCheck(prev => prev ? {
          ...prev,
          ...updates,
          rating: updates.rating === null ? undefined : updates.rating,
        } : null);
        message.success('更新成功');
      }
    } catch {
      message.error('更新失败');
    }
  };

  const handleAddEmotionRecord = async (values: { episode: number; emotionLevel: number }) => {
    if (!favoriteCheck?.favoriteId) return;
    try {
      const res = await request.post('/emotioncurves', {
        favoriteId: favoriteCheck.favoriteId,
        episode: values.episode,
        emotionLevel: values.emotionLevel,
      }) as any;
      if (res.code === 200) {
        message.success('情感记录已保存');
        setEmotionModalVisible(false);
        emotionForm.resetFields();
        loadEmotionCurves();
      }
    } catch {
      message.error('保存失败');
    }
  };

  const handleDeleteReview = async () => {
    if (!favoriteCheck?.favoriteId) return;
    try {
      const res = await request.delete(`/reviews/${favoriteCheck.favoriteId}`) as any;
      if (res.code === 200) {
        message.success('已删除观后感');
        setReview(null);
      }
    } catch {
      message.error('删除失败');
    }
  };

  const getEmotionChartOption = () => {
    const sortedData = [...emotionCurves].sort((a, b) => a.episode - b.episode);
    return {
      grid: {
        left: 50,
        right: 20,
        top: 20,
        bottom: 40,
      },
      xAxis: {
        type: 'category',
        data: sortedData.map(d => `第${d.episode}集`),
        axisLabel: {
          fontSize: 11,
          color: '#6B6B6B',
        },
        axisLine: {
          lineStyle: { color: '#E8E4DE' },
        },
      },
      yAxis: {
        type: 'value',
        min: 0,
        max: 5,
        interval: 1,
        axisLabel: {
          fontSize: 11,
          color: '#6B6B6B',
          formatter: (value: number) => {
            const labels = ['', '平淡', '', '波动', '', '强烈'];
            return labels[value] || value;
          },
        },
        splitLine: {
          lineStyle: { color: '#F0EFED' },
        },
      },
      series: [
        {
          type: 'line',
          data: sortedData.map(d => d.emotionLevel),
          smooth: true,
          symbol: 'circle',
          symbolSize: 8,
          lineStyle: {
            color: '#D4A574',
            width: 2,
          },
          itemStyle: {
            color: '#D4A574',
          },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0,
              y: 0,
              x2: 0,
              y2: 1,
              colorStops: [
                { offset: 0, color: 'rgba(212, 165, 116, 0.3)' },
                { offset: 1, color: 'rgba(212, 165, 116, 0.05)' },
              ],
            },
          },
        },
      ],
    };
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

  if (!anime) {
    return (
      <div className={styles.container}>
        <main className={styles.main}>
          <div className={styles.emptyState}>
            <p>动漫不存在</p>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <main className={styles.main}>
        <div className={styles.breadcrumb}>
          <Breadcrumb
            items={[
              { title: <a href="/">首页</a> },
              { title: anime.name },
            ]}
          />
        </div>

        <div className={styles.content}>
          <div className={styles.coverSection}>
            {anime.cover ? (
              <img src={anime.cover} alt={anime.name} className={styles.cover} />
            ) : (
              <div className={styles.coverPlaceholder}>
                <span>暂无封面</span>
              </div>
            )}

            {/* 基本信息卡 — Bangumi 元信息 */}
            {(anime.airDate || anime.duration || anime.producer || anime.director ||
              anime.bangumiScore || anime.bangumiRank) && (
              <div className={styles.favoritePanel} style={{ marginTop: 24 }}>
                <div className={styles.favoritePanelTitle}>基本信息</div>
                <div className={styles.favoritePanelContent}>
                  {anime.airDate && (
                    <div className={styles.favoriteRow}>
                      <span className={styles.favoriteLabel}>放送日期</span>
                      <span>{anime.airDate}</span>
                    </div>
                  )}
                  {anime.duration && (
                    <div className={styles.favoriteRow}>
                      <span className={styles.favoriteLabel}>片长</span>
                      <span>{anime.duration}</span>
                    </div>
                  )}
                  {anime.producer && (
                    <div className={styles.favoriteRow}>
                      <span className={styles.favoriteLabel}>制作</span>
                      <span>{anime.producer}</span>
                    </div>
                  )}
                  {anime.director && (
                    <div className={styles.favoriteRow}>
                      <span className={styles.favoriteLabel}>监督</span>
                      <span>{anime.director}</span>
                    </div>
                  )}
                  {anime.bangumiScore && (
                    <div className={styles.favoriteRow}>
                      <span className={styles.favoriteLabel}>评分</span>
                      <span>
                        {anime.bangumiScore.toFixed(1)}
                        {anime.bangumiRatingCount && ` (${anime.bangumiRatingCount} 人)`}
                      </span>
                    </div>
                  )}
                  {anime.bangumiRank && (
                    <div className={styles.favoriteRow}>
                      <span className={styles.favoriteLabel}>排名</span>
                      <span>#{anime.bangumiRank}</span>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* 标签卡 — Bangumi 官方 Top 5 标签 */}
            {anime.tags && anime.tags.length > 0 && (
              <div className={styles.favoritePanel} style={{ marginTop: 24 }}>
                <div className={styles.favoritePanelTitle}>标签</div>
                <div className={styles.favoritePanelContent}>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
                    {anime.tags.slice(0, 5).map((tag) => (
                      <Tag key={tag.name}>{tag.name}</Tag>
                    ))}
                  </div>
                </div>
              </div>
            )}
          </div>

          <div className={styles.infoSection}>
            <h1 className={styles.title}>{anime.name}</h1>

            <div className={styles.stats}>
              <span className={styles.statItem}>{anime.animeType}</span>
              {anime.totalEpisodes && anime.totalEpisodes > 0 && (
                <>
                  <span className={styles.statDivider}>|</span>
                  <span className={styles.statItem}>{anime.totalEpisodes} 集</span>
                </>
              )}
              <span className={styles.statDivider}>|</span>
              <span className={styles.statItem}>{anime.favoriteCount} 人追番</span>
              {anime.avgRating && (
                <>
                  <span className={styles.statDivider}>|</span>
                  <span className={styles.statItem}>
                    <span className={styles.star}>★</span>
                    {anime.avgRating.toFixed(1)}
                  </span>
                </>
              )}
              <span className={styles.statDivider}>|</span>
              <span className={styles.statItem}>{anime.reviewCount} 篇观后感</span>
            </div>

            <div className={styles.actions}>
              <Button
                type={favoriteCheck?.isFavorited ? 'default' : 'primary'}
                loading={favoriteLoading}
                onClick={handleToggleFavorite}
              >
                {favoriteCheck?.isFavorited ? '取消追番' : '追番'}
              </Button>
            </div>

            {favoriteCheck?.isFavorited && (
              <div className={styles.favoritePanel}>
                <div className={styles.favoritePanelTitle}>追番状态</div>
                <div className={styles.favoritePanelContent}>
                  <div className={styles.favoriteRow}>
                    <span className={styles.favoriteLabel}>状态</span>
                    <Select
                      className={styles.statusSelect}
                      value={favoriteCheck.status}
                      options={STATUS_OPTIONS}
                      onChange={(value) => handleUpdateFavorite({ status: value })}
                    />
                  </div>
                  <div className={styles.favoriteRow}>
                    <span className={styles.favoriteLabel}>进度</span>
                    <InputNumber
                      className={styles.episodeInput}
                      min={0}
                      max={anime.totalEpisodes && anime.totalEpisodes > 0 ? anime.totalEpisodes : 500}
                      value={favoriteCheck.progress || 0}
                      onChange={(value) => handleUpdateFavorite({ progress: value || 0 })}
                    />
                  </div>
                  <div className={styles.favoriteRow}>
                    <span className={styles.favoriteLabel}>评分</span>
                    <Rate
                      className={styles.ratingRate}
                      allowHalf={false}
                      value={Math.round((favoriteCheck.rating || 0) / 2)}
                      onChange={(value) => {
                        const score = Math.round(value) * 2;
                        handleUpdateFavorite({ rating: score || null });
                      }}
                    />
                  </div>
                </div>
              </div>
            )}

            {anime.summary && (
              <div className={styles.summary}>
                <h3 className={styles.summaryTitle}>简介</h3>
                <p className={styles.summaryText}>{anime.summary}</p>
              </div>
            )}

            {favoriteCheck?.isFavorited && (
              <div className={styles.detailTabs}>
                <Tabs
                  defaultActiveKey="tags"
                  items={[
                    {
                      key: 'tags',
                      label: '情感标签',
                      children: (
                        <div className={styles.tabContent}>
                          {favoriteCheck?.isFavorited && (
                            <div className={styles.tagInputRow}>
                              <Input
                                placeholder="输入新标签名"
                                value={newTagName}
                                onChange={(e) => setNewTagName(e.target.value)}
                                onPressEnter={handleAddTag}
                                className={styles.tagInput}
                              />
                              <Button type="primary" onClick={handleAddTag}>添加</Button>
                            </div>
                          )}
                          <div className={styles.tagList}>
                            {emotionTags.map((tag) => (
                              <Tag
                                key={tag.id === 0 ? `preset-${tag.name}` : tag.id}
                                color={tag.isPreset ? 'default' : 'processing'}
                                closable={!tag.isPreset}
                                onClose={() => !tag.isPreset && handleDeleteTag(tag.id)}
                              >
                                {tag.name}
                              </Tag>
                            ))}
                          </div>
                        </div>
                      ),
                    },
                    {
                      key: 'emotion',
                      label: '情感曲线',
                      children: (
                        <div className={styles.tabContent}>
                          <div className={styles.tabHeader}>
                            <Button type="primary" onClick={() => setEmotionModalVisible(true)}>
                              记录情感
                            </Button>
                          </div>
                          {emotionCurves.length > 0 ? (
                            <div className={styles.emotionChart}>
                              <ReactECharts option={getEmotionChartOption()} />
                            </div>
                          ) : (
                            <div className={styles.emptyTab}>
                              <p>还没有情感记录</p>
                              <p className={styles.emptyHint}>点击"记录情感"开始记录追番时的情绪波动</p>
                            </div>
                          )}
                        </div>
                      ),
                    },
                    {
                      key: 'review',
                      label: '观后感',
                      children: (
                        <div className={styles.tabContent}>
                          <div className={styles.tabHeader}>
                            <Button type="primary" onClick={() => setReviewModalVisible(true)}>
                              {review ? '编辑观后感' : '写观后感'}
                            </Button>
                          </div>
                          {review ? (
                            <div className={styles.reviewContent}>
                              <div className={styles.reviewText}>
                                <ReactMarkdown>{review.content}</ReactMarkdown>
                              </div>
                              <div className={styles.reviewMeta}>
                                <span>发布于 {new Date(review.createTime).toLocaleDateString()}</span>
                                {review.updatedAt !== review.createTime && (
                                  <span>，编辑于 {new Date(review.updatedAt).toLocaleDateString()}</span>
                                )}
                              </div>
                              <Popconfirm
                                title="确定删除这篇观后感？"
                                onConfirm={handleDeleteReview}
                                okText="确定"
                                cancelText="取消"
                              >
                                <Button danger size="small">删除</Button>
                              </Popconfirm>
                            </div>
                          ) : (
                            <div className={styles.emptyTab}>
                              <p>还没有观后感</p>
                              <p className={styles.emptyHint}>写一段追番感想，记录你的追番历程</p>
                            </div>
                          )}
                        </div>
                      ),
                    },
                  ]}
                />
              </div>
            )}
          </div>
        </div>
      </main>

      <Modal
        title="记录情感"
        open={emotionModalVisible}
        onCancel={() => setEmotionModalVisible(false)}
        footer={null}
      >
        <Form
          form={emotionForm}
          layout="vertical"
          onFinish={handleAddEmotionRecord}
          initialValues={{ episode: 1, emotionLevel: 3 }}
        >
          <Form.Item
            name="episode"
            label="集数"
            rules={[{ required: true, message: '请选择集数' }]}
          >
            <InputNumber
              min={1}
              max={anime.totalEpisodes && anime.totalEpisodes > 0 ? anime.totalEpisodes : 500}
              className={styles.modalInput}
            />
          </Form.Item>
          <Form.Item
            name="emotionLevel"
            label="情绪等级"
            rules={[{ required: true, message: '请选择情绪等级' }]}
          >
            <Select
              className={styles.modalInput}
              options={EMOTION_LEVELS.map(l => ({ value: l.value, label: l.label }))}
            />
          </Form.Item>
          <Form.Item className={styles.modalFooter}>
            <Space>
              <Button onClick={() => setEmotionModalVisible(false)}>取消</Button>
              <Button type="primary" htmlType="submit">保存</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      <ReviewModal
        visible={reviewModalVisible}
        review={review}
        favoriteId={favoriteCheck?.favoriteId ?? 0}
        onClose={() => setReviewModalVisible(false)}
        onSaved={loadReview}
      />
    </div>
  );
}
