import { AppApiError, parseApiError, getBengaliErrorMessage } from './errors';

export function getApiBaseUrl() {
  if (typeof window !== 'undefined' && window.__API_URL__) {
    return window.__API_URL__.replace(/\/+$/, '');
  }
  return 'http://localhost:8080';
}

async function request(path, options = {}) {
  const url = `${getApiBaseUrl()}${path.startsWith('/') ? path : `/${path}`}`;
  const headers = new Headers(options.headers || {});

  if (!headers.has('Content-Type') && options.body && typeof options.body === 'string') {
    headers.set('Content-Type', 'application/json');
  }

  try {
    const res = await fetch(url, {
      ...options,
      headers,
    });

    if (!res.ok) {
      throw await parseApiError(res);
    }

    if (res.status === 204) {
      return undefined;
    }

    return await res.json();
  } catch (err) {
    if (err instanceof AppApiError) {
      throw err;
    }

    if (err instanceof DOMException && err.name === 'AbortError') {
      throw err;
    }

    throw new AppApiError('network_error', getBengaliErrorMessage('network_error'), 0);
  }
}

export const api = {
  getClasses: (signal) => {
    return request('/classes', { method: 'GET', signal });
  },

  getClassById: (classId, signal) => {
    return request(`/classes/${classId}`, { method: 'GET', signal });
  },

  createClass: (payload) => {
    return request('/classes', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  createHold: (classId, studentId) => {
    return request(`/classes/${classId}/holds`, {
      method: 'POST',
      headers: {
        'X-Student-Id': studentId,
      },
    });
  },

  getHoldById: (holdId, signal) => {
    return request(`/holds/${holdId}`, { method: 'GET', signal });
  },

  confirmHold: (holdId, studentId) => {
    return request(`/holds/${holdId}/confirm`, {
      method: 'POST',
      headers: {
        'X-Student-Id': studentId,
      },
    });
  },

  cancelHold: (holdId, studentId) => {
    return request(`/holds/${holdId}`, {
      method: 'DELETE',
      headers: {
        'X-Student-Id': studentId,
      },
    });
  },

  getMyHolds: (studentId, signal) => {
    return request('/me/holds', {
      method: 'GET',
      headers: {
        'X-Student-Id': studentId,
      },
      signal,
    });
  },
};
