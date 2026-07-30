import { Component } from 'react';
import type { ErrorInfo, ReactNode } from 'react';
import { Button, MessageBar, MessageBarBody, MessageBarTitle } from '@fluentui/react-components';

interface Props {
  children: ReactNode;
}

interface State {
  error: Error | null;
}

/**
 * Without this, any render error unmounts the whole tree and leaves a blank white page with
 * nothing but a console message. Must be a class — React has no hook equivalent.
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unhandled render error:', error, info.componentStack);
  }

  render() {
    if (!this.state.error) {
      return this.props.children;
    }

    return (
      <div style={{ padding: '24px', maxWidth: '720px', margin: '0 auto' }}>
        <MessageBar intent="error">
          <MessageBarBody>
            <MessageBarTitle>Something broke while rendering this page.</MessageBarTitle>
            {this.state.error.message}
          </MessageBarBody>
        </MessageBar>

        <div style={{ marginTop: '16px', display: 'flex', gap: '8px' }}>
          <Button appearance="primary" onClick={() => this.setState({ error: null })}>
            Try again
          </Button>
          <Button onClick={() => window.location.assign('/installations')}>
            Back to installations
          </Button>
        </div>
      </div>
    );
  }
}
