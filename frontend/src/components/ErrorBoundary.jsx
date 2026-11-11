import React, { Component } from 'react';

export class ErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = {
      hasError: false,
      errorMessage: null,
    };
  }

  static getDerivedStateFromError(error) {
    return {
      hasError: true,
      errorMessage: error ? error.toString() : 'অপ্রত্যাশিত সমস্যা দেখা দিয়েছে',
    };
  }

  componentDidCatch(error, errorInfo) {
    console.error('Uncaught error caught by ErrorBoundary:', error, errorInfo);
  }

  handleReload = () => {
    window.location.reload();
  };

  render() {
    if (this.state.hasError) {
      return (
        <div className="error-boundary-container">
          <div className="error-boundary-card">
            <h2>কিছু একটা ভুল হয়েছে</h2>
            <p>অ্যাপ্লিকেশনে অপ্রত্যাশিত সমস্যা দেখা দিয়েছে। পৃষ্ঠাটি রিলোড করে আবার চেষ্টা করুন।</p>
            {this.state.errorMessage && (
              <pre className="error-details">{this.state.errorMessage}</pre>
            )}
            <button type="button" className="btn btn-primary" onClick={this.handleReload}>
              পৃষ্ঠা রিলোড দিন
            </button>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
