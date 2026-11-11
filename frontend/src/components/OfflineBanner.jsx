import React from 'react';

export const OfflineBanner = ({ isOffline }) => {
  if (!isOffline) return null;

  return (
    <div className="offline-banner" role="alert">
      <span className="banner-text">
        সার্ভার চালু নেই বা সংযোগ বিচ্ছিন্ন। সর্বশেষ প্রাপ্ত তথ্য দেখানো হচ্ছে...
      </span>
    </div>
  );
};
