import { Card, Tag, Progress, Tooltip } from 'antd';
import type { RecommendItem, RecommendResult } from '../../types/api';
import styles from './RecommendAnimeCard.module.css';

interface Props {
  item: RecommendItem;
  mode: RecommendResult['mode'];
  compact?: boolean;
  onClick?: (item: RecommendItem) => void;
}

export function RecommendAnimeCard({ item, mode, compact, onClick }: Props) {
  const canClick = item.animeId !== null;

  const handleClick = () => {
    if (canClick && onClick) onClick(item);
  };

  const scorePercent = Math.round(item.score * 100);
  const showDetail = mode !== 'popular';

  const breakdownLines: string[] = [];
  if (showDetail) {
    breakdownLines.push(`标签重合 ${Math.round(item.breakdown.tagOverlap * 100)}%`);
    if (mode === 'full') {
      breakdownLines.push(`情绪相近 ${Math.round(item.breakdown.emotionAffinity * 100)}%`);
    }
    breakdownLines.push(`质量 ${Math.round(item.breakdown.qualityBoost * 100)}%`);
    if (item.breakdown.nearestNeighborName) {
      breakdownLines.push(`最近: ${item.breakdown.nearestNeighborName}`);
    }
  }

  const tagsToShow = item.tags.slice(0, 3);
  const tagsOverflow = item.tags.length - 3;

  return (
    <Card
      className={`${styles.card} ${!canClick ? styles.cardDisabled : ''} ${compact ? styles.compact : ''}`}
      hoverable={canClick}
      onClick={handleClick}
      cover={
        item.cover ? (
          <img src={item.cover} alt={item.name} className={styles.cover} />
        ) : (
          <div className={styles.coverPlaceholder}>暂无封面</div>
        )
      }
    >
      {item.animeType && (
        <span className={styles.typeBadge}>{item.animeType}</span>
      )}

      <Card.Meta
        title={<div className={styles.name}>{item.name}</div>}
        description={
          <div className={styles.tagsRow}>
            {tagsToShow.map((t) => <Tag key={t}>{t}</Tag>)}
            {tagsOverflow > 0 && <Tag>+{tagsOverflow}</Tag>}
          </div>
        }
      />

      {showDetail && (
        <>
          <div className={styles.reason}>{item.reason}</div>
          <Tooltip title={breakdownLines.map((l, i) => <div key={i}>{l}</div>)}>
            <div className={styles.scoreRow}>
              <Progress
                percent={scorePercent}
                strokeColor="#D4A574"
                size="small"
                showInfo={false}
              />
              <span className={styles.scoreText}>{scorePercent}%</span>
            </div>
          </Tooltip>
        </>
      )}

      {!canClick && (
        <div className={styles.noLocalBadge}>暂无本地记录</div>
      )}
    </Card>
  );
}
