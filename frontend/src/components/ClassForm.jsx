import React, { useState } from 'react';
import { api } from '../api/client';
import { AppApiError } from '../api/errors';

export const ClassForm = ({ onClassCreated }) => {
  const [name, setName] = useState('');
  const [totalSeats, setTotalSeats] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState(null);
  const [successMsg, setSuccessMsg] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setErrorMsg(null);
    setSuccessMsg(null);

    const trimmedName = name.trim();
    if (!trimmedName) {
      setErrorMsg('ক্লাসের নাম দিন');
      return;
    }

    const seatsNum = Number(totalSeats);
    if (!totalSeats || isNaN(seatsNum) || seatsNum <= 0 || seatsNum > 100000) {
      setErrorMsg('সিট সংখ্যা ১ থেকে ১০০০০০ এর মধ্যে হতে হবে');
      return;
    }

    setIsLoading(true);
    try {
      await api.createClass({
        name: trimmedName,
        totalSeats: seatsNum,
      });

      setSuccessMsg(`"${trimmedName}" ক্লাস সফলভাবে তৈরি হয়েছে!`);
      setName('');
      setTotalSeats('');
      onClassCreated();
    } catch (err) {
      if (err instanceof AppApiError) {
        setErrorMsg(err.message);
      } else {
        setErrorMsg('ক্লাস তৈরি করা যায়নি, আবার চেষ্টা করুন');
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <section className="card class-form-card">
      <h2>নতুন ক্লাস যোগ করুন</h2>
      <form onSubmit={handleSubmit} className="class-form">
        <div className="form-row">
          <div className="form-group">
            <label htmlFor="class-name">ক্লাসের নাম:</label>
            <input
              id="class-name"
              type="text"
              placeholder="Class 6 - Morning Shift"
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={isLoading}
              maxLength={200}
              required
            />
          </div>

          <div className="form-group">
            <label htmlFor="total-seats">মোট আসন (সিট):</label>
            <input
              id="total-seats"
              type="number"
              placeholder="উদা: 40"
              value={totalSeats}
              onChange={(e) => setTotalSeats(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
              disabled={isLoading}
              min={1}
              max={100000}
              required
            />
          </div>

          <div className="form-actions">
            <button
              type="submit"
              className="btn btn-primary"
              disabled={isLoading || !name.trim() || !totalSeats}
            >
              {isLoading ? 'তৈরি হচ্ছে...' : 'ক্লাস তৈরি করুন'}
            </button>
          </div>
        </div>

        {errorMsg && (
          <div className="alert alert-danger" role="alert">
            {errorMsg}
          </div>
        )}
        {successMsg && (
          <div className="alert alert-success" role="status">
            {successMsg}
          </div>
        )}
      </form>
    </section>
  );
};
