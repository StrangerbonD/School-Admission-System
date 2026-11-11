import { useState, useCallback } from 'react';

const STORAGE_KEY = 'school_admission_student_id';
const STUDENT_ID_REGEX = /^[0-9]{17}$/;

export function useStudentId() {
  const [studentId, setStudentIdState] = useState(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) || '';
    } catch {
      return '';
    }
  });

  const setStudentId = useCallback((value) => {
    const trimmed = value.trim();
    setStudentIdState(trimmed);
    try {
      localStorage.setItem(STORAGE_KEY, trimmed);
    } catch {
    }
  }, []);

  const isValid = STUDENT_ID_REGEX.test(studentId);

  let validationError = null;
  if (!studentId) {
    validationError = '১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন';
  } else if (!/^[0-9]*$/.test(studentId)) {
    validationError = 'শুধুমাত্র ইংরেজি সংখ্যা [0-9] ব্যবহার করুন';
  } else if (studentId.length !== 17) {
    validationError = `১৭ অঙ্কের হতে হবে (বর্তমানে ${studentId.length} অঙ্ক)`;
  }

  return {
    studentId,
    setStudentId,
    isValid,
    validationError: isValid ? null : validationError,
  };
}
