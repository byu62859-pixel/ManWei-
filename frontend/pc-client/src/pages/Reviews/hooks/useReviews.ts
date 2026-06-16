import { useEffect, useCallback } from 'react';
import { useReviewsStore } from '../store/reviewsStore';

export function useReviews() {
  const store = useReviewsStore();

  const fetchReviews = useCallback(() => {
    store.fetchReviews();
  }, [store.fetchReviews]);

  useEffect(() => {
    fetchReviews();
  }, [fetchReviews]);

  return {
    ...store,
    fetchReviews,
  };
}