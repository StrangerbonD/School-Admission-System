import { useState, useEffect, useRef, useCallback } from 'react';

export function usePolling(fetchFn, intervalMs = 3000) {
  const [data, setData] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isOffline, setIsOffline] = useState(false);

  const abortControllerRef = useRef(null);
  const seqRef = useRef(0);
  const inFlightRef = useRef(false);
  const isMountedRef = useRef(true);

  const executeFetch = useCallback(async () => {
    if (inFlightRef.current) {
      return;
    }

    inFlightRef.current = true;
    const currentSeq = ++seqRef.current;

    abortControllerRef.current?.abort();
    const controller = new AbortController();
    abortControllerRef.current = controller;

    try {
      const result = await fetchFn(controller.signal);

      if (!isMountedRef.current || currentSeq !== seqRef.current) {
        return;
      }

      setData(result);
      setIsOffline(false);
    } catch (err) {
      if (!isMountedRef.current || currentSeq !== seqRef.current) {
        return;
      }

      if (err instanceof DOMException && err.name === 'AbortError') {
        return;
      }

      setIsOffline(true);
    } finally {
      if (isMountedRef.current && currentSeq === seqRef.current) {
        setIsLoading(false);
      }
      inFlightRef.current = false;
    }
  }, [fetchFn]);

  const refresh = useCallback(async () => {
    await executeFetch();
  }, [executeFetch]);

  useEffect(() => {
    isMountedRef.current = true;
    executeFetch();

    const intervalId = setInterval(() => {
      executeFetch();
    }, intervalMs);

    return () => {
      isMountedRef.current = false;
      clearInterval(intervalId);
      abortControllerRef.current?.abort();
    };
  }, [executeFetch, intervalMs]);

  return {
    data,
    isLoading,
    isOffline,
    refresh,
  };
}
