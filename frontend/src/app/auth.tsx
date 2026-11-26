import { createContext, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { api, getSession, onSessionChange, setSession } from '../api/client';
import type { AuthResponse, User } from '../api/types';

interface AuthValue {
  user: User | null;
  isAdmin: boolean;
  login: (email: string, password: string) => Promise<User>;
  register: (fullName: string, email: string, password: string) => Promise<User>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const qc = useQueryClient();
  const [user, setUser] = useState<User | null>(getSession()?.user ?? null);

  // Сессию может закрыть и клиент API (refresh-токен не обменялся) — тогда пользователь выходит везде.
  useEffect(() => onSessionChange((s) => setUser(s?.user ?? null)), []);

  // После входа или выхода чужие данные в кэше не нужны. Только при настоящей смене пользователя, не при загрузке:
  // открытые экраны перезапрашивают свои данные (reset), а неоткрытые просто выбрасываются.
  const prevUser = useRef(user?.id);
  useEffect(() => {
    if (prevUser.current === user?.id) return;
    prevUser.current = user?.id;
    const personal = (q: { queryKey: readonly unknown[] }) => !['categories', 'locations', 'features'].includes(String(q.queryKey[0]));
    qc.removeQueries({ type: 'inactive', predicate: personal });
    qc.resetQueries({ type: 'active', predicate: personal });
  }, [user?.id, qc]);

  const value = useMemo<AuthValue>(() => ({
    user,
    isAdmin: user?.role === 'Admin',
    async login(email, password) {
      const auth = await api<AuthResponse>('/auth/login', { method: 'POST', body: { email, password } });
      setSession(auth);
      return auth.user;
    },
    async register(fullName, email, password) {
      const auth = await api<AuthResponse>('/auth/register', { method: 'POST', body: { fullName, email, password } });
      setSession(auth);
      return auth.user;
    },
    async logout() {
      const refreshToken = getSession()?.refreshToken;
      setSession(null);
      if (refreshToken) {
        await api('/auth/logout', { method: 'POST', body: { refreshToken } }).catch(() => undefined);
      }
    },
  }), [user]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth вне AuthProvider');
  return ctx;
}
