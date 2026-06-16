import { useEffect } from 'react';
import { useDataCenterStore } from '../../stores/dataCenterStore';
import { EmotionCurveTimeline } from './components/EmotionCurveTimeline';
import { WordCloud } from './components/WordCloud';
import { HabitAnalysis } from './components/HabitAnalysis';
import { StatCard } from './components/StatCard';
import styles from './DataCenter.module.css';

export function DataCenter() {
  const { userStats, wordCloudData, emotionCurves, loading, error, fetchAll } = useDataCenterStore();

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>加载中...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>{error}</div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <header className={styles.header}>
        <h1 className={styles.title}>数据中心</h1>
        <p className={styles.subtitle}>您的追番情绪与习惯分析</p>
      </header>

      <div className={styles.statsRow}>
        <StatCard
          label="总集数"
          value={userStats?.totalEpisodes ?? 0}
          subLabel="观看过的总集数"
        />
        <StatCard
          label="平均评分"
          value={userStats?.avgRating?.toFixed(1) ?? '-'}
          subLabel="综合评分 1-10"
        />
        <StatCard
          label="观后感"
          value={userStats?.reviewCount ?? 0}
          subLabel="撰写数量"
        />
        <StatCard
          label="情绪标签"
          value={wordCloudData.length}
          subLabel="使用过的标签数"
        />
      </div>

      <div className={styles.content}>
        <div className={styles.mainChart}>
          <h2 className={styles.chartTitle}>情感曲线时间轴</h2>
          <EmotionCurveTimeline data={emotionCurves} />
        </div>

        <div className={styles.chartCard}>
          <h2 className={styles.chartTitle}>情绪词云</h2>
          <WordCloud data={wordCloudData} />
        </div>

        <div className={styles.chartCard}>
          <h2 className={styles.chartTitle}>追番习惯分析</h2>
          <HabitAnalysis emotionCurves={emotionCurves} />
        </div>
      </div>
    </div>
  );
}