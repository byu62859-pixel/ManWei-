import { Select } from 'antd';

interface FilterBarProps {
  orderBy: string;
  onOrderByChange: (orderBy: string) => void;
}

const ORDER_OPTIONS = [
  { value: 'CreateTime', label: '按时间排序' },
  { value: 'Rating', label: '按评分排序' },
  { value: 'AnimeName', label: '按名称排序' },
];

export function FilterBar({ orderBy, onOrderByChange }: FilterBarProps) {
  return (
    <div style={{ marginBottom: 24 }}>
      <Select
        value={orderBy}
        options={ORDER_OPTIONS}
        onChange={onOrderByChange}
        style={{ width: 160 }}
      />
    </div>
  );
}