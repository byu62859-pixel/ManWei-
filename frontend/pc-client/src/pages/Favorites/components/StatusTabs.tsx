import { Tabs } from 'antd';
import type { FavoriteStatus } from '../types';

interface StatusTabsProps {
  activeStatus: FavoriteStatus | null;
  onChange: (status: FavoriteStatus | null) => void;
  counts: {
    all: number;
    wish: number;
    watching: number;
    watched: number;
  };
}

export function StatusTabs({ activeStatus, onChange, counts }: StatusTabsProps) {
  const items = [
    {
      key: 'null',
      label: `全部 (${counts.all})`,
    },
    {
      key: '0',
      label: `想看 (${counts.wish})`,
    },
    {
      key: '1',
      label: `在看 (${counts.watching})`,
    },
    {
      key: '2',
      label: `看过 (${counts.watched})`,
    },
  ];

  return (
    <Tabs
      activeKey={String(activeStatus)}
      onChange={(key) => {
        onChange(key === 'null' ? null : (Number(key) as FavoriteStatus));
      }}
      items={items}
    />
  );
}