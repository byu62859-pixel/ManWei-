import { useEffect, useRef, useMemo } from 'react';
import * as echarts from 'echarts';
import type { EmotionCurveWithAnime } from '../../../../types/api';
import styles from './HabitAnalysis.module.css';

interface HabitAnalysisProps {
  emotionCurves: EmotionCurveWithAnime[];
}

interface TypeEmotionStat {
  type: string;
  avgEmotion: number;
  count: number;
}

export function HabitAnalysis({ emotionCurves }: HabitAnalysisProps) {
  const chartRef = useRef<HTMLDivElement>(null);
  const chartInstance = useRef<echarts.ECharts | undefined>(undefined);

  const chartData = useMemo(() => {
    const typeMap = new Map<string, { total: number; count: number }>();

    emotionCurves.forEach((curve) => {
      const existing = typeMap.get(curve.animeType) || { total: 0, count: 0 };
      existing.total += curve.emotionLevel;
      existing.count += 1;
      typeMap.set(curve.animeType, existing);
    });

    const stats: TypeEmotionStat[] = [];
    typeMap.forEach((value, key) => {
      stats.push({
        type: key,
        avgEmotion: value.total / value.count,
        count: value.count,
      });
    });

    return stats.sort((a, b) => b.avgEmotion - a.avgEmotion);
  }, [emotionCurves]);

  useEffect(() => {
    if (!chartRef.current) return;

    chartInstance.current = echarts.init(chartRef.current);

    const option: echarts.EChartsOption = {
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: 'shadow' },
        formatter: (params: any) => {
          const data = params[0];
          const avg = typeof data.value === 'number' ? data.value.toFixed(1) : data.value;
          return `<strong>${data.name}</strong><br/>平均情绪: ${avg}<br/>记录数: ${data.data.count}`;
        },
      },
      grid: {
        left: 80,
        right: 40,
        top: 24,
        bottom: 40,
      },
      xAxis: {
        type: 'value',
        name: '平均情绪等级',
        min: 1,
        max: 5,
        axisLine: { lineStyle: { color: '#E8E4DE' } },
        axisLabel: { color: '#6B6B6B' },
        splitLine: { lineStyle: { color: '#F0EFED' } },
      },
      yAxis: {
        type: 'category',
        data: chartData.map((d) => d.type),
        axisLine: { lineStyle: { color: '#E8E4DE' } },
        axisLabel: { color: '#6B6B6B', fontSize: 12 },
      },
      series: [
        {
          type: 'bar',
          data: chartData.map((d) => ({
            value: d.avgEmotion.toFixed(1),
            count: d.count,
          })),
          barWidth: 20,
          itemStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 1, 0, [
              { offset: 0, color: '#D4A574' },
              { offset: 1, color: '#E8C9A8' },
            ]),
            borderRadius: [0, 4, 4, 0],
          },
        },
      ],
    };

    chartInstance.current.setOption(option);

    const handleResize = () => chartInstance.current?.resize();
    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
      chartInstance.current?.dispose();
    };
  }, [chartData]);

  if (chartData.length === 0) {
    return (
      <div className={styles.empty}>
        <span className={styles.emptyIcon}>🧠</span>
        <p className={styles.emptyText}>暂无习惯数据</p>
        <p className={styles.emptyHint}>记录更多动漫的情绪后，分析将展示您的追番偏好</p>
      </div>
    );
  }

  return <div ref={chartRef} className={styles.chart} />;
}