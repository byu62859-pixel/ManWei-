import { useEffect, useRef } from 'react';
import * as echarts from 'echarts';
import type { EmotionCurveWithAnime } from '../../../../types/api';
import styles from './EmotionCurveTimeline.module.css';

interface EmotionCurveTimelineProps {
  data: EmotionCurveWithAnime[];
}

export function EmotionCurveTimeline({ data }: EmotionCurveTimelineProps) {
  const chartRef = useRef<HTMLDivElement>(null);
  const chartInstance = useRef<echarts.ECharts | undefined>(undefined);

  useEffect(() => {
    if (!chartRef.current) return;

    chartInstance.current = echarts.init(chartRef.current);

    const groupedData = data.reduce((acc, curve) => {
      if (!acc[curve.animeName]) {
        acc[curve.animeName] = [];
      }
      acc[curve.animeName].push([curve.episode, curve.emotionLevel] as [number, number]);
      return acc;
    }, {} as Record<string, [number, number][]>);

    const series = Object.entries(groupedData).map(([animeName, points]) => ({
      name: animeName,
      type: 'line' as const,
      smooth: true,
      symbol: 'circle',
      symbolSize: 6,
      lineStyle: { width: 2 },
      data: points.sort((a, b) => a[0] - b[0]),
    }));

    const option: echarts.EChartsOption = {
      tooltip: {
        trigger: 'item' as const,
        formatter: (params: any) => {
          if (!params.data) return '';
          return `<strong>${params.seriesName}</strong><br/>集数: ${params.data[0]}<br/>情绪等级: ${params.data[1]}`;
        },
      },
      legend: {
        top: 0,
        type: 'scroll' as const,
        textStyle: { color: '#6B6B6B', fontSize: 12 },
      },
      grid: {
        left: 40,
        right: 24,
        top: 48,
        bottom: 48,
      },
      xAxis: {
        type: 'value' as const,
        name: '集数',
        nameLocation: 'middle' as const,
        nameGap: 30,
        axisLine: { lineStyle: { color: '#E8E4DE' } },
        axisLabel: { color: '#6B6B6B' },
        splitLine: { lineStyle: { color: '#F0EFED' } },
      },
      yAxis: {
        type: 'value' as const,
        name: '情绪',
        min: 1,
        max: 5,
        interval: 1,
        axisLine: { lineStyle: { color: '#E8E4DE' } },
        axisLabel: {
          color: '#6B6B6B',
          formatter: (value: string | number) => {
            const labels: Record<number, string> = { 1: '平静', 2: '', 3: '轻微', 4: '', 5: '强烈' };
            return labels[Number(value)] || String(value);
          },
        },
        splitLine: { lineStyle: { color: '#F0EFED' } },
      },
      dataZoom: [
        { type: 'inside' as const, start: 0, end: 100 },
        {
          type: 'slider' as const,
          start: 0,
          end: 100,
          height: 20,
          bottom: 8,
          borderColor: '#E8E4DE',
          fillerColor: 'rgba(212, 165, 116, 0.1)',
          handleStyle: { color: '#D4A574' },
        },
      ],
      series,
      color: ['#D4A574', '#C49664', '#B08A5A', '#9E7E50', '#8A7246'],
    };

    chartInstance.current.setOption(option);

    const handleResize = () => chartInstance.current?.resize();
    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
      chartInstance.current?.dispose();
    };
  }, [data]);

  if (data.length === 0) {
    return (
      <div className={styles.empty}>
        <span className={styles.emptyIcon}>&#x1F4C8;</span>
        <p className={styles.emptyText}>暂无情感曲线</p>
        <p className={styles.emptyHint}>收藏动漫并记录观看情绪后，将展示您的追番情感波动</p>
      </div>
    );
  }

  return <div ref={chartRef} className={styles.chart} />;
}