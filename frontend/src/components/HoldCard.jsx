import React, { useState } from 'react';
import { useCountdown } from '../hooks/useCountdown';
import { api } from '../api/client';
import { AppApiError } from '../api/errors';

export const HoldCard = ({
  hold,
  studentId,
  onActionComplete,
}) => {
  const [isConfirming, setIsConfirming] = useState(false);
  const [isCancelling, setIsCancelling] = useState(false);
  const [errorMessage, setErrorMessage] = useState(null);

  const { formattedTime, isExpired } = useCountdown(
    hold.expiresAt,
    hold.serverTime,
    hold.status,
    onActionComplete
  );

  const handleConfirm = async () => {
    setIsConfirming(true);
    setErrorMessage(null);

    try {
      await api.confirmHold(hold.holdId, studentId);
      onActionComplete();
    } catch (err) {
      if (err instanceof AppApiError) {
        setErrorMessage(err.message);
      } else {
        setErrorMessage('ভর্তি নিশ্চিত করা সম্ভব হয়নি');
      }
      onActionComplete();
    } finally {
      setIsConfirming(false);
    }
  };

  const handleCancel = async () => {
    setIsCancelling(true);
    setErrorMessage(null);

    try {
      await api.cancelHold(hold.holdId, studentId);
      onActionComplete();
    } catch (err) {
      if (err instanceof AppApiError) {
        setErrorMessage(err.message);
      } else {
        setErrorMessage('আবেদন বাতিল করা সম্ভব হয়নি');
      }
      onActionComplete();
    } finally {
      setIsCancelling(false);
    }
  };

  const displayStatus = isExpired ? 'EXPIRED' : hold.status;

  return (
    <div className={`hold-card status-${displayStatus.toLowerCase()}`}>
      <div className="hold-card-header">
        <h3 className="hold-class-name">{hold.className}</h3>
        <span className={`status-pill pill-${displayStatus.toLowerCase()}`}>
          {displayStatus === 'HELD' && 'হোল্ডে রয়েছে (HELD)'}
          {displayStatus === 'CONFIRMED' && 'ভর্তি নিশ্চিত (CONFIRMED)'}
          {displayStatus === 'EXPIRED' && 'মেয়াদ শেষ (EXPIRED)'}
          {displayStatus === 'CANCELLED' && 'বাতিলকৃত (CANCELLED)'}
        </span>
      </div>

      <div className="hold-card-body">
        {displayStatus === 'HELD' && (
          <div className="countdown-box">
            <span className="countdown-label">আসন ধরে রাখার সময় বাকি:</span>
            <span className="countdown-timer">{formattedTime}</span>
          </div>
        )}

        {displayStatus === 'CONFIRMED' && (
          <div className="status-notice notice-confirmed">
            আপনার এই ক্লাসে ভর্তি সফলভাবে নিশ্চিত হয়েছে।
          </div>
        )}

        {displayStatus === 'EXPIRED' && (
          <div className="status-notice notice-expired">
            এই আবেদনের সময়সীমা শেষ হয়ে গেছে। সিট পুনরায় উন্মুক্ত হয়েছে।
          </div>
        )}

        {displayStatus === 'CANCELLED' && (
          <div className="status-notice notice-cancelled">
            এই আবেদনটি বাতিল করা হয়েছে।
          </div>
        )}

        {errorMessage && (
          <div className="alert alert-danger" role="alert">
            {errorMessage}
          </div>
        )}
      </div>

      {(displayStatus === 'HELD' || displayStatus === 'EXPIRED') && (
        <div className="hold-card-actions">
          <button
            type="button"
            className="btn btn-primary btn-confirm"
            onClick={handleConfirm}
            disabled={isConfirming || isCancelling || isExpired || displayStatus === 'EXPIRED'}
          >
            {isConfirming ? 'ফি জমা হচ্ছে...' : 'ভর্তি ফি দিন'}
          </button>

          {displayStatus === 'HELD' && (
            <button
              type="button"
              className="btn btn-outline-danger btn-cancel"
              onClick={handleCancel}
              disabled={isConfirming || isCancelling || isExpired}
            >
              {isCancelling ? 'বাতিল হচ্ছে...' : 'বাতিল করুন'}
            </button>
          )}
        </div>
      )}
    </div>
  );
};
