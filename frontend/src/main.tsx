import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ApiError } from './api/client';
import { App } from './App';
import { AuthProvider } from './app/auth';
import { LiveProvider } from './app/live';
import { ThemeProvider } from './app/theme';
import { ToastProvider } from './app/toast';
import { SearchProvider } from './features/search/state';
import './styles/tokens.css';
import './styles/components.css';
import './styles/screens.css';
import './styles/layout.css';
import './styles/pages.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 20_000,
      refetchOnWindowFocus: true,
      // Ошибки 4xx повторять бессмысленно, а сетевые сбои — стоит пару раз.
      retry: (count, error) => !(error instanceof ApiError && error.status >= 400 && error.status < 500) && count < 2,
    },
  },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <QueryClientProvider client={queryClient}>
        <ThemeProvider>
          <ToastProvider>
            <AuthProvider>
              <LiveProvider>
                <SearchProvider>
                  <App />
                </SearchProvider>
              </LiveProvider>
            </AuthProvider>
          </ToastProvider>
        </ThemeProvider>
      </QueryClientProvider>
    </BrowserRouter>
  </StrictMode>,
);
