import { useEffect, useMemo, useRef, useState } from 'react';
import type { WordCloudItem } from '../../../../types/api';
import styles from './WordCloud.module.css';

interface WordCloudProps {
  data: WordCloudItem[];
  onTagClick?: (tag: string) => void;
}

const PALETTE = [
  'rgba(77, 145, 137, 0.92)',
  'rgba(130, 184, 220, 0.72)',
  'rgba(238, 137, 58, 0.82)',
  'rgba(213, 83, 130, 0.74)',
  'rgba(216, 174, 66, 0.78)',
  'rgba(132, 92, 68, 0.72)',
  'rgba(127, 188, 173, 0.66)',
  'rgba(234, 166, 181, 0.58)',
];

const WORD_SLOTS = [
  { left: 48, top: 47, width: 30, size: 48, color: 0 },
  { left: 71, top: 43, width: 25, size: 42, color: 1 },
  { left: 20, top: 45, width: 27, size: 40, color: 2 },
  { left: 70, top: 77, width: 25, size: 38, color: 3 },
  { left: 21, top: 73, width: 20, size: 32, color: 4 },
  { left: 84, top: 68, width: 15, size: 22, color: 5 },
  { left: 36, top: 17, width: 18, size: 24, color: 4 },
  { left: 14, top: 27, width: 15, size: 17, color: 6 },
  { left: 27, top: 34, width: 14, size: 18, color: 5 },
  { left: 63, top: 25, width: 12, size: 13, color: 7 },
  { left: 47, top: 70, width: 13, size: 14, color: 6 },
  { left: 80, top: 30, width: 12, size: 13, color: 3 },
  { left: 60, top: 49, width: 11, size: 13, color: 7 },
  { left: 39, top: 30, width: 11, size: 12, color: 3 },
  { left: 55, top: 37, width: 11, size: 11, color: 5 },
  { left: 31, top: 27, width: 11, size: 11, color: 7 },
  { left: 78, top: 62, width: 10, size: 10, color: 6 },
  { left: 21, top: 58, width: 11, size: 11, color: 3 },
];

const hashWord = (word: string) => {
  let hash = 0;
  for (let i = 0; i < word.length; i += 1) {
    hash = word.charCodeAt(i) + ((hash << 5) - hash);
  }
  return Math.abs(hash);
};

// 字符宽度估算：按 Unicode 码点分档（规避打包环境正则编码问题）
const estimateCharWidth = (ch: string): number => {
  const code = ch.codePointAt(0) ?? 0;
  // CJK 统一表意文字：基本区 + 扩展 A 区
  if ((code >= 0x4e00 && code <= 0x9fff) || (code >= 0x3400 && code <= 0x4dbf)) return 1.0;
  // 日文平假名/片假名
  if ((code >= 0x3040 && code <= 0x30ff) || (code >= 0xff65 && code <= 0xff9f)) return 0.95;
  // 韩文
  if ((code >= 0xac00 && code <= 0xd7af) || (code >= 0x1100 && code <= 0x11ff)) return 1.0;
  // 全角符号
  if (code >= 0xff00 && code <= 0xffef) return 1.0;
  // 半角字母数字
  if ((code >= 0x30 && code <= 0x39) ||
      (code >= 0x41 && code <= 0x5a) ||
      (code >= 0x61 && code <= 0x7a)) return 0.55;
  // 半角空格
  if (code === 0x20) return 0.3;
  // 半角标点
  if ('·.,!?:;\'"-_/()[]{}'.indexOf(ch) >= 0) return 0.5;
  return 0.6;
};

const estimateTextWidth = (text: string, fontSize: number): number => {
  return [...text].reduce((sum, ch) => sum + estimateCharWidth(ch) * fontSize, 0);
};

const CONTAINER_HEIGHT = 300;
const PAD_X = 12;
const PAD_Y = 24;

export function WordCloud({ data, onTagClick }: WordCloudProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [containerWidth, setContainerWidth] = useState(600);

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    setContainerWidth(el.getBoundingClientRect().width);
    const ro = new ResizeObserver((entries) => {
      const w = entries[0]?.contentRect.width ?? 0;
      if (w > 0) setContainerWidth(w);
    });
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const words = useMemo(() => {
    if (data.length === 0 || containerWidth <= 0) return [];

    const sorted = [...data].sort((a, b) => b.count - a.count);
    const maxCount = sorted[0].count;
    const minCount = sorted[sorted.length - 1].count;
    const countRange = Math.max(maxCount - minCount, 1);

    const drawWidth = containerWidth - PAD_X * 2;
    const drawHeight = CONTAINER_HEIGHT - PAD_Y * 2;

    // 已放置词的 AABB 矩形列表
    type Rect = { left: number; top: number; right: number; bottom: number };
    const placed: Rect[] = [];

    // AABB 碰撞检测（带 2px 间距，避免紧贴视觉粘连）
    const GAP = 2;
    const intersects = (a: Rect, b: Rect) =>
      a.left < b.right + GAP &&
      a.right + GAP > b.left &&
      a.top < b.bottom + GAP &&
      a.bottom + GAP > b.top;

    // 将候选中心点 clamp 到安全区
    const clampToBounds = (cx: number, cy: number, w: number, h: number) => {
      const halfW = w / 2;
      const halfH = h / 2;
      let nx = cx;
      let ny = cy;
      if (nx - halfW < PAD_X) nx = PAD_X + halfW;
      if (nx + halfW > containerWidth - PAD_X) nx = containerWidth - PAD_X - halfW;
      if (ny - halfH < PAD_Y) ny = PAD_Y + halfH;
      if (ny + halfH > CONTAINER_HEIGHT - PAD_Y) ny = CONTAINER_HEIGHT - PAD_Y - halfH;
      return { nx, ny };
    };

    // 螺旋搜索：尝试放置一个矩形
    // 从中心点开始，按螺旋外扩，步长 4-8px，最多 30 次
    const tryPlace = (cx: number, cy: number, w: number, h: number): { cx: number; cy: number } | null => {
      const initial = clampToBounds(cx, cy, w, h);
      const tryRect = (ccx: number, ccy: number): { cx: number; cy: number } | null => {
        const { nx, ny } = clampToBounds(ccx, ccy, w, h);
        const rect: Rect = {
          left: nx - w / 2,
          right: nx + w / 2,
          top: ny - h / 2,
          bottom: ny + h / 2,
        };
        for (const p of placed) {
          if (intersects(rect, p)) return null;
        }
        return { cx: nx, cy: ny };
      };

      // 第一次尝试：原始位置
      const first = tryRect(initial.nx, initial.ny);
      if (first) return first;

      // 螺旋外扩
      const step = 5;
      for (let i = 1; i <= 30; i += 1) {
        // 阿基米德螺旋：angle 步进，半径随 i 增大
        const angle = i * 0.6;
        const radius = step * i;
        const dx = Math.cos(angle) * radius;
        const dy = Math.sin(angle) * radius;
        const result = tryRect(initial.nx + dx, initial.ny + dy);
        if (result) return result;
      }
      return null;
    };

    const placedWords: Array<{
      name: string;
      count: number;
      leftPercent: number;
      topPercent: number;
      fontSize: number;
      color: string;
      opacity: number;
    }> = [];

    for (let idx = 0; idx < Math.min(30, sorted.length); idx += 1) {
      const item = sorted[idx];
      const ratio = (item.count - minCount) / countRange;
      const slot = WORD_SLOTS[idx % WORD_SLOTS.length];
      const cycle = Math.floor(idx / WORD_SLOTS.length);
      const hashed = hashWord(item.name);

      // 初始目标位置
      const baseCx = cycle === 0
        ? (slot.left / 100) * containerWidth
        : PAD_X + ((hashed % 72) / 100) * drawWidth;
      const baseCy = cycle === 0
        ? (slot.top / 100) * CONTAINER_HEIGHT
        : PAD_Y + (((hashed >> 3) % 64) / 100) * drawHeight;

      // 字号只由词频权重决定
      const baseSize = cycle === 0 ? slot.size : Math.round(11 + ratio * 14);
      const color = PALETTE[(cycle === 0 ? slot.color : hashed) % PALETTE.length];
      const opacity = cycle === 0 ? 1 : 0.58;

      // 第一次尝试：原始字号
      let finalSize = Math.max(10, Math.min(baseSize, 56));
      let w = estimateTextWidth(item.name, finalSize);
      let h = finalSize;
      let placedPos = tryPlace(baseCx, baseCy, w, h);

      // 第二次尝试：缩小字号到 85% 后重试
      if (!placedPos) {
        finalSize = Math.max(10, Math.round(finalSize * 0.85));
        w = estimateTextWidth(item.name, finalSize);
        h = finalSize;
        placedPos = tryPlace(baseCx, baseCy, w, h);
      }

      // 仍失败 → 跳过该词
      if (!placedPos) continue;

      // 记录矩形与渲染项
      placed.push({
        left: placedPos.cx - w / 2,
        right: placedPos.cx + w / 2,
        top: placedPos.cy - h / 2,
        bottom: placedPos.cy + h / 2,
      });
      placedWords.push({
        name: item.name,
        count: item.count,
        leftPercent: (placedPos.cx / containerWidth) * 100,
        topPercent: (placedPos.cy / CONTAINER_HEIGHT) * 100,
        fontSize: finalSize,
        color,
        opacity,
      });
    }

    return placedWords;
  }, [data, containerWidth]);

  if (data.length === 0) {
    return (
      <div className={styles.empty}>
        <span className={styles.emptyIcon}>&#x2601;</span>
        <p className={styles.emptyText}>暂无情绪标签</p>
        <p className={styles.emptyHint}>观看动漫并记录情感后，词云将展示您的追番情绪分布</p>
      </div>
    );
  }

  return (
    <div ref={containerRef} className={styles.container}>
      {words.map((item) => (
        <button
          key={item.name}
          type="button"
          className={styles.word}
          style={{
            left: `${item.leftPercent}%`,
            top: `${item.topPercent}%`,
            fontSize: `${item.fontSize}px`,
            color: item.color,
            opacity: item.opacity,
          }}
          onClick={() => onTagClick?.(item.name)}
          title={`${item.name}：${item.count}`}
        >
          {item.name}
        </button>
      ))}
    </div>
  );
}
