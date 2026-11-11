export class AppApiError extends Error {
  constructor(errorCode, message, statusCode) {
    super(message);
    this.name = 'AppApiError';
    this.errorCode = errorCode;
    this.statusCode = statusCode;
  }
}

export const ERROR_MESSAGES_BN = {
  invalid_student_id: 'ভুল জন্ম নিবন্ধন নম্বর',
  validation_error: 'ভুল ইনপুট',
  not_found: 'পাওয়া যায়নি',
  forbidden: 'এই আবেদন আপনার নয়',
  method_not_allowed: 'ভুল অনুরোধ',
  no_seats: 'সিট শেষ',
  already_applied: 'আপনার ইতিমধ্যে আবেদন আছে',
  hold_expired: 'মেয়াদ শেষ',
  hold_cancelled: 'এই আবেদন বাতিল হয়ে গেছে',
  already_confirmed: 'ভর্তি ইতিমধ্যে নিশ্চিত, বাতিল করা যাবে না',
  service_unavailable: 'সার্ভার ব্যস্ত, একটু পরে চেষ্টা করুন',
  internal_error: 'কিছু একটা ভুল হয়েছে, আবার চেষ্টা করুন',
  network_error: 'সার্ভার চালু নেই',
};

export function getBengaliErrorMessage(errorCode, statusCode) {
  if (ERROR_MESSAGES_BN[errorCode]) {
    return ERROR_MESSAGES_BN[errorCode];
  }

  if (statusCode === 404) return 'অনুরোধকৃত তথ্য পাওয়া যায়নি';
  if (statusCode === 403) return 'অনুমতি নেই';
  if (statusCode === 409) return 'তথ্যগত বিরোধ বা অসামঞ্জস্য তৈরি হয়েছে';
  if (statusCode === 400) return 'অনুরোধটি সঠিক নয়';
  if (statusCode && statusCode >= 500) return 'সার্ভারে সমস্যা হয়েছে, কিছুক্ষণ পর চেষ্টা করুন';

  return 'একটি অপ্রত্যাশিত ত্রুটি ঘটেছে';
}

export async function parseApiError(response) {
  try {
    const data = await response.json();
    const code = data.error || 'internal_error';
    const message = getBengaliErrorMessage(code, response.status);
    return new AppApiError(code, message, response.status);
  } catch {
    const code = response.status >= 500 ? 'internal_error' : 'validation_error';
    const message = getBengaliErrorMessage(code, response.status);
    return new AppApiError(code, message, response.status);
  }
}
