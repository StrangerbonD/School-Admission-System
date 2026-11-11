import React from 'react';
import { HoldCard } from './HoldCard';

export const MyHolds = ({
  holds,
  studentId,
  isStudentIdValid,
  isLoading,
  onActionComplete,
}) => {
  if (!isStudentIdValid) {
    return (
      <section className="card my-holds-card">
        <h2>আমার আবেদনসমূহ</h2>
        <div className="info-box">
          <p>আপনার আবেদনগুলোর বর্তমান অবস্থা দেখতে উপরে আপনার ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।</p>
        </div>
      </section>
    );
  }

  return (
    <section className="card my-holds-card">
      <div className="section-header">
        <h2>আমার আবেদনসমূহ ({holds.length})</h2>
        {isLoading && <span className="loading-spinner-small">লোড হচ্ছে...</span>}
      </div>

      {holds.length === 0 ? (
        <div className="empty-state">
          <p>এই শিক্ষার্থীর নামে কোনো চলমান বা অতীত আবেদন পাওয়া যায়নি। নিচের ক্লাসের তালিকা থেকে আবেদন করতে পারেন।</p>
        </div>
      ) : (
        <div className="holds-grid">
          {holds.map((hold) => (
            <HoldCard
              key={hold.holdId}
              hold={hold}
              studentId={studentId}
              onActionComplete={onActionComplete}
            />
          ))}
        </div>
      )}
    </section>
  );
};
