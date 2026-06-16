import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Input, Card, Pagination, Spin, message } from 'antd';
import request from '../../services/request';
import type { Anime } from '../../types/api';
import styles from './Home.module.css';

const { Search } = Input;

export function Home() {
  const navigate = useNavigate();
  const [animeList, setAnimeList] = useState<Anime[]>([]);
  const [loading, setLoading] = useState(false);
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [total, setTotal] = useState(0);

  const loadAnime = useCallback(async () => {
    setLoading(true);
    try {
      const res = await request.get('/anime', {
        params: { Page: page, PageSize: pageSize, Keyword: keyword || undefined },
      }) as any;
      if (res.code === 200) {
        setAnimeList(res.data.items);
        setTotal(res.data.totalCount);
      }
    } catch {
      message.error('获取动漫列表失败');
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, keyword]);

  useEffect(() => {
    loadAnime();
  }, [loadAnime]);

  const handleSearch = (value: string) => {
    setKeyword(value);
    setPage(1);
  };

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
  };

  const handleAnimeClick = (id: number) => {
    navigate(`/anime/${id}`);
  };

  return (
    <div className={styles.container}>
      <main className={styles.main}>
        <div className={styles.mainContent}>
          <div className={styles.searchArea}>
            <Search
              placeholder="搜索动漫名称..."
              enterButton="搜索"
              size="large"
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              onSearch={handleSearch}
              className={styles.searchInput}
            />
          </div>

          <Spin spinning={loading}>
            <div className={styles.animeGrid}>
              {animeList.map((anime) => (
                <Card
                  key={anime.id}
                  className={styles.animeCard}
                  cover={
                    anime.cover ? (
                      <img
                        src={anime.cover}
                        alt={anime.name}
                        className={styles.animeCover}
                      />
                    ) : (
                      <div className={styles.coverPlaceholder}>
                        <span>暂无封面</span>
                      </div>
                    )
                  }
                  onClick={() => handleAnimeClick(anime.id)}
                  hoverable
                >
                  <Card.Meta
                    title={<div className={styles.animeTitle}>{anime.name}</div>}
                    description={
                      <div className={styles.animeInfo}>
                        <span className={styles.animeType}>{anime.animeType}</span>
                        <span className={styles.animeStats}>
                          {anime.favoriteCount} 人追番
                        </span>
                      </div>
                    }
                  />
                  {anime.avgRating && (
                    <div className={styles.rating}>
                      <span className={styles.ratingStar}>★</span>
                      <span>{anime.avgRating.toFixed(1)}</span>
                    </div>
                  )}
                </Card>
              ))}
            </div>
          </Spin>

          {animeList.length === 0 && !loading && (
            <div className={styles.emptyState}>
              <p>暂无动漫数据</p>
            </div>
          )}

          {total > 0 && (
            <div className={styles.pagination}>
              <Pagination
                current={page}
                pageSize={pageSize}
                total={total}
                onChange={handlePageChange}
                showSizeChanger={false}
                showTotal={(t) => `共 ${t} 部动漫`}
              />
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
