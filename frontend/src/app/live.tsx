import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { accessToken } from '../api/client';
import { invalidateBookings, keys } from '../api/hooks';
import type { Notification } from '../api/types';
import { useAuth } from './auth';
import { useToast } from './toast';

/**
 * Живые обновления (SignalR, PROJECT.md 7.4). Пока пользователь вошёл, держится одно соединение:
 * личные уведомления приходят сами, а экраны расписания и схемы мест подписываются на свои группы.
 * На событие экран не перерисовывается вручную: данные помечаются устаревшими, и TanStack Query перечитывает их.
 */

interface LiveValue {
  connected: boolean;
  /** Колокольчик качнулся — пришло новое уведомление. */
  ring: number;
  /** Последний ресурс и коворкинг, расписание которых изменилось, — для подсказки «расписание обновилось само». */
  lastChange: { resourceId?: number; spaceId?: number; at: number } | null;
  subscribe: (kind: 'Resource' | 'Space', id: number) => () => void;
}

const LiveContext = createContext<LiveValue>({ connected: false, ring: 0, lastChange: null, subscribe: () => () => undefined });

export function LiveProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const qc = useQueryClient();
  const toast = useToast();
  const conn = useRef<HubConnection | null>(null);
  const subs = useRef(new Map<string, number>());
  const [connected, setConnected] = useState(false);
  const [ring, setRing] = useState(0);
  const [lastChange, setLastChange] = useState<LiveValue['lastChange']>(null);

  useEffect(() => {
    if (!user) return;

    const hub = new HubConnectionBuilder()
      .withUrl('/hubs/live', { accessTokenFactory: async () => (await accessToken()) ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    conn.current = hub;

    hub.on('notificationReceived', (n: Notification) => {
      qc.invalidateQueries({ queryKey: keys.unread });
      invalidateBookings(qc);
      setRing((r) => r + 1);
      toast('notice', n.title, n.message);
    });
    hub.on('scheduleChanged', ({ resourceId }: { resourceId: number }) => {
      qc.invalidateQueries({ queryKey: ['schedule', resourceId] });
      qc.invalidateQueries({ queryKey: ['available'] });
      setLastChange({ resourceId, at: Date.now() });
    });
    hub.on('spaceChanged', ({ spaceId }: { spaceId: number }) => {
      qc.invalidateQueries({ queryKey: ['space', spaceId] });
      qc.invalidateQueries({ queryKey: ['spaces'] });
      setLastChange({ spaceId, at: Date.now() });
    });

    // После переподключения сервер забыл группы — подписываемся заново на всё, что открыто.
    const resubscribe = async () => {
      setConnected(true);
      for (const key of subs.current.keys()) {
        const [kind, id] = key.split(':');
        await hub.invoke(`SubscribeTo${kind}`, Number(id)).catch(() => undefined);
      }
      invalidateBookings(qc);
    };
    hub.onreconnected(resubscribe);
    hub.onreconnecting(() => setConnected(false));
    hub.onclose(() => setConnected(false));

    hub.start().then(resubscribe).catch(() => setConnected(false));

    return () => {
      conn.current = null;
      setConnected(false);
      hub.stop();
    };
  }, [user?.id, qc, toast]);

  const subscribe = useCallback<LiveValue['subscribe']>((kind, id) => {
    const key = `${kind}:${id}`;
    subs.current.set(key, (subs.current.get(key) ?? 0) + 1);
    const hub = conn.current;
    if (hub?.state === HubConnectionState.Connected) hub.invoke(`SubscribeTo${kind}`, id).catch(() => undefined);

    return () => {
      const left = (subs.current.get(key) ?? 1) - 1;
      if (left > 0) {
        subs.current.set(key, left);
        return;
      }
      subs.current.delete(key);
      if (conn.current?.state === HubConnectionState.Connected) conn.current.invoke(`UnsubscribeFrom${kind}`, id).catch(() => undefined);
    };
  }, []);

  const value = useMemo(() => ({ connected, ring, lastChange, subscribe }), [connected, ring, lastChange, subscribe]);
  return <LiveContext.Provider value={value}>{children}</LiveContext.Provider>;
}

export const useLive = () => useContext(LiveContext);

/** Подписка экрана на изменения ресурса или коворкинга, пока экран открыт. */
export function useLiveSubscription(kind: 'Resource' | 'Space', id: number | undefined) {
  const { subscribe } = useLive();
  // Подписка запоминается сразу, даже если соединение ещё не готово: провайдер подпишется при подключении
  // и после каждого переподключения.
  useEffect(() => (id ? subscribe(kind, id) : undefined), [kind, id, subscribe]);
}
