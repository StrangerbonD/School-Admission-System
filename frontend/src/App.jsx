import React, { useState, useEffect, useCallback } from 'react';
import { Header } from './components/Header';
import { OfflineBanner } from './components/OfflineBanner';
import { MyHolds } from './components/MyHolds';
import { ClassList } from './components/ClassList';
import { ClassForm } from './components/ClassForm';
import { useStudentId } from './hooks/useStudentId';
import { usePolling } from './hooks/usePolling';
import { api } from './api/client';
import './App.css';

export const App = () => {
  const [activeTab, setActiveTab] = useState('student');
  const { studentId, setStudentId, isValid: isStudentIdValid, validationError } = useStudentId();

  const [myHolds, setMyHolds] = useState([]);
  const [isMyHoldsLoading, setIsMyHoldsLoading] = useState(false);

  const fetchClassesCallback = useCallback((signal) => api.getClasses(signal), []);
  const {
    data: classes,
    isLoading: isClassesLoading,
    isOffline,
    refresh: refreshClasses,
  } = usePolling(fetchClassesCallback, 3000);

  const fetchMyHolds = useCallback(async (signal, silent = false) => {
    if (!isStudentIdValid) {
      setMyHolds([]);
      return;
    }
    if (!silent) {
      setIsMyHoldsLoading(true);
    }
    try {
      const list = await api.getMyHolds(studentId, signal);
      setMyHolds(list);
    } catch {
    } finally {
      if (!silent) {
        setIsMyHoldsLoading(false);
      }
    }
  }, [studentId, isStudentIdValid]);

  useEffect(() => {
    if (!isStudentIdValid) {
      setMyHolds([]);
      return;
    }
    const controller = new AbortController();
    fetchMyHolds(controller.signal, false);

    const intervalId = window.setInterval(() => {
      fetchMyHolds(controller.signal, true);
    }, 3000);

    return () => {
      window.clearInterval(intervalId);
      controller.abort();
    };
  }, [fetchMyHolds, isStudentIdValid]);

  const refreshAll = useCallback(async () => {
    await Promise.all([
      refreshClasses(),
      fetchMyHolds(undefined, true),
    ]);
  }, [refreshClasses, fetchMyHolds]);

  return (
    <div className="app-container">
      <Header
        activeTab={activeTab}
        studentId={studentId}
        onStudentIdChange={setStudentId}
        isValid={isStudentIdValid}
        validationError={validationError}
      />

      <nav className="app-nav" aria-label="প্রধান নেভিগেশন">
        <button
          type="button"
          className={`nav-tab-btn ${activeTab === 'student' ? 'active' : ''}`}
          onClick={() => setActiveTab('student')}
        >
          শিক্ষার্থী ও অভিভাবক কর্নার
        </button>
        <button
          type="button"
          className={`nav-tab-btn ${activeTab === 'admin' ? 'active' : ''}`}
          onClick={() => setActiveTab('admin')}
        >
          স্কুল অ্যাডমিন প্যানেল
        </button>
      </nav>

      <OfflineBanner isOffline={isOffline} />

      <main className="main-content">
        {activeTab === 'student' ? (
          <>
            <MyHolds
              holds={myHolds}
              studentId={studentId}
              isStudentIdValid={isStudentIdValid}
              isLoading={isMyHoldsLoading}
              onActionComplete={refreshAll}
            />

            <ClassList
              classes={classes || []}
              studentId={studentId}
              isStudentIdValid={isStudentIdValid}
              onHoldCreated={refreshAll}
              isLoading={isClassesLoading}
              isAdminView={false}
              onSwitchToAdmin={() => setActiveTab('admin')}
            />
          </>
        ) : (
          <>
            <ClassForm onClassCreated={refreshClasses} />

            <ClassList
              classes={classes || []}
              studentId={studentId}
              isStudentIdValid={isStudentIdValid}
              onHoldCreated={refreshAll}
              isLoading={isClassesLoading}
              isAdminView={true}
            />
          </>
        )}
      </main>
    </div>
  );
};

export default App;
