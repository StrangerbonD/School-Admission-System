import React from 'react';

export const Header = ({
  activeTab,
  studentId,
  onStudentIdChange,
  isValid,
  validationError,
}) => {
  return (
    <header className="app-header">
      <div className="header-brand">
        <h1>বিদ্যালয় ভর্তি সিস্টেম</h1>
      </div>

      {activeTab === 'student' ? (
        <div className="student-id-container">
          <label htmlFor="student-id-input" className="student-id-label">
            শিক্ষার্থীর জন্ম নিবন্ধন নম্বর:
          </label>
          <div className="student-id-input-group">
            <input
              id="student-id-input"
              type="text"
              inputMode="numeric"
              maxLength={17}
              placeholder="১৭ অঙ্কের জন্ম নিবন্ধন নম্বর"
              value={studentId}
              onChange={(e) => onStudentIdChange(e.target.value)}
              className={`student-id-input ${isValid ? 'is-valid' : studentId ? 'is-invalid' : ''}`}
            />
            {isValid && (
              <span className="badge badge-success" title="১৭ অঙ্ক সঠিক">
                যাচাইকৃত
              </span>
            )}
          </div>
          {validationError && (
            <span className="validation-message" role="alert">
              {validationError}
            </span>
          )}
        </div>
      ) : (
        <div className="admin-header-info">
          <span className="badge badge-admin">অ্যাডমিন মোড</span>
        </div>
      )}
    </header>
  );
};
