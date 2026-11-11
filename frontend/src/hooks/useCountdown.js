import { useState, useEffect, useRef } from 'react';

export function useCountdown(expiresAt, serverTime, status, onExpire) {
  const offsetRef = useRef(0);
  const onExpireCalledRef = useRef(false);

  useEffect(() => {
    try {
      const serverMs = Date.parse(serverTime);
      if (!isNaN(serverMs)) {
        offsetRef.current = serverMs - Date.now();
      }
    } catch {
      offsetRef.current = 0;
    }
    onExpireCalledRef.current = false;
  }, [serverTime, expiresAt]);

  const calculateRemaining = () => {
    if (status !== 'HELD') {
      return 0;
    }

    try {
      const expireMs = Date.parse(expiresAt);
      if (isNaN(expireMs)) return 0;

      const nowWithOffset = Date.now() + offsetRef.current;
      const remainingMs = expireMs - nowWithOffset;
      return Math.max(0, Math.ceil(remainingMs / 1000));
    } catch {
      return 0;
    }
  };

  const [remainingSeconds, setRemainingSeconds] = useState(calculateRemaining);

  useEffect(() => {
    if (status !== 'HELD') {
      setRemainingSeconds(0);
      return;
    }

    const initial = calculateRemaining();
    setRemainingSeconds(initial);

    const intervalId = setInterval(() => {
      const remaining = calculateRemaining();
      setRemainingSeconds(remaining);

      if (remaining <= 0 && !onExpireCalledRef.current) {
        onExpireCalledRef.current = true;
        clearInterval(intervalId);
        onExpire?.();
      }
    }, 1000);

    return () => clearInterval(intervalId);
  }, [expiresAt, serverTime, status, onExpire]);

  const isExpired = status === 'EXPIRED' || (status === 'HELD' && remainingSeconds <= 0);

  const minutes = Math.floor(remainingSeconds / 60);
  const seconds = remainingSeconds % 60;
  const formattedTime = `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;

  return {
    remainingSeconds,
    formattedTime,
    isExpired,
  };
}
