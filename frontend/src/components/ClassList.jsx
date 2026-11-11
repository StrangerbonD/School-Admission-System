import React, { useState } from 'react';
import { api } from '../api/client';
import { AppApiError } from '../api/errors';

export const ClassList = ({
  classes,
  studentId,
  isStudentIdValid,
  onHoldCreated,
  isLoading,
  isAdminView = false,
  onSwitchToAdmin,
}) => {
  const [applyingClassId, setApplyingClassId] = useState(null);
  const [actionError, setActionError] = useState(null);

  const handleApply = async (classItem) => {
    if (!isStudentIdValid) {
      setActionError({
        classId: classItem.id,
        message: 'আবেদনের পূর্বে উপরে ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন',
      });
      return;
    }

    setApplyingClassId(classItem.id);
    setActionError(null);

    try {
      await api.createHold(classItem.id, studentId);
      onHoldCreated();
    } catch (err) {
      if (err instanceof AppApiError) {
        setActionError({ classId: classItem.id, message: err.message });
      } else {
        setActionError({ classId: classItem.id, message: 'আবেদন করা সম্ভব হয়নি' });
      }
    } finally {
      setApplyingClassId(null);
    }
  };

  return (
    <section className="card class-list-card">
      <div className="section-header">
        <h2>
          {isAdminView
            ? 'ভর্তির জন্য উন্মুক্ত ক্লাসের তালিকা ও আসনের লাইভ অবস্থা'
            : 'ভর্তির জন্য উন্মুক্ত ক্লাসের তালিকা'}
        </h2>
        {isLoading && <span className="loading-spinner-small">আপডেট হচ্ছে...</span>}
      </div>

      {classes.length === 0 ? (
        <div className="empty-state">
          <p>
            {isAdminView
              ? 'বর্তমানে কোনো ক্লাস তৈরি করা হয়নি। উপরের ফর্ম থেকে নতুন ক্লাস যোগ করুন।'
              : 'বর্তমানে ভর্তির জন্য কোনো ক্লাস তৈরি করা হয়নি।'}
          </p>
          {!isAdminView && onSwitchToAdmin && (
            <div style={{ marginTop: '0.75rem' }}>
              <button
                type="button"
                className="btn btn-secondary"
                onClick={onSwitchToAdmin}
              >
                নতুন ক্লাস তৈরি করুন (স্কুল অ্যাডমিন প্যানেল)
              </button>
            </div>
          )}
        </div>
      ) : (
        <div className="table-responsive">
          <table className="class-table">
            <thead>
              <tr>
                <th>ক্লাসের নাম</th>
                <th className="text-center">মোট সিট</th>
                <th className="text-center">হোল্ডে</th>
                <th className="text-center">নিশ্চিত ভর্তি</th>
                <th className="text-center">খালি সিট</th>
                <th className="text-center">{isAdminView ? 'ভর্তি অবস্থা' : 'পদক্ষেপ'}</th>
              </tr>
            </thead>
            <tbody>
              {classes.map((cls) => {
                const isApplyingThis = applyingClassId === cls.id;
                const isFull = cls.availableSeats <= 0;
                const classErr = actionError?.classId === cls.id ? actionError.message : null;

                return (
                  <tr key={cls.id} className={isFull ? 'row-full' : ''}>
                    <td className="class-name-cell">
                      <strong>{cls.name}</strong>
                      {classErr && <div className="cell-error">{classErr}</div>}
                    </td>
                    <td className="text-center">{cls.totalSeats}</td>
                    <td className="text-center">
                      <span className="badge badge-held">{cls.heldSeats}</span>
                    </td>
                    <td className="text-center">
                      <span className="badge badge-confirmed">{cls.confirmedSeats}</span>
                    </td>
                    <td className="text-center">
                      <span
                        className={`badge ${
                          cls.availableSeats > 0 ? 'badge-available' : 'badge-empty'
                        }`}
                      >
                        {cls.availableSeats}
                      </span>
                    </td>
                    <td className="text-center">
                      {isAdminView ? (
                        <span
                          className={`badge ${
                            cls.availableSeats > 0 ? 'badge-available' : 'badge-empty'
                          }`}
                        >
                          {cls.availableSeats > 0 ? 'ভর্তি উন্মুক্ত' : 'আসন পূর্ণ'}
                        </span>
                      ) : (
                        <button
                          type="button"
                          className="btn btn-apply"
                          onClick={() => handleApply(cls)}
                          disabled={isApplyingThis || isFull || !isStudentIdValid}
                          title={
                            !isStudentIdValid
                              ? 'প্রথমে ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন'
                              : isFull
                              ? 'কোনো আসন খালি নেই'
                              : 'আসন হোল্ড করার জন্য আবেদন করুন'
                          }
                        >
                          {isApplyingThis ? 'আবেদন হচ্ছে...' : isFull ? 'সিট নেই' : 'আবেদন করুন'}
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
};
