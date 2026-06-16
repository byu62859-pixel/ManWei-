import { useEffect, useCallback } from 'react';
import { useFavoritesStore } from '../store/favoritesStore';

export function useFavorites() {
  const store = useFavoritesStore();

  const fetchFavorites = useCallback(() => {
    store.fetchFavorites();
  }, [store.fetchFavorites]);

  const fetchCounts = useCallback(() => {
    store.fetchCounts();
  }, [store.fetchCounts]);

  useEffect(() => {
    fetchFavorites();
    fetchCounts();
  }, [fetchFavorites, fetchCounts]);

  return {
    ...store,
    fetchFavorites,
    fetchCounts,
  };
}