import { keepPreviousData, useInfiniteQuery, useQuery, type QueryClient } from '@tanstack/react-query';
import { api, type Query } from './client';
import type {
  AdminStats, AuditLog, AvailableResource, Booking, Category, Feature, Location, Notification, Paged, Resource,
  ResourceSchedule, Space, Suggestion, User,
} from './types';

/**
 * Запросы к API через TanStack Query: он кэширует ответы, показывает старые данные, пока грузятся новые,
 * и умеет «пометить устаревшим» — так живые события SignalR обновляют экран одним вызовом invalidate.
 */

export const keys = {
  me: ['me'] as const,
  categories: ['categories'] as const,
  locations: ['locations'] as const,
  features: ['features'] as const,
  resources: (q: Query) => ['resources', q] as const,
  resource: (id: number) => ['resource', id] as const,
  available: (q: Query) => ['available', q] as const,
  schedule: (id: number, from: string, to: string) => ['schedule', id, from, to] as const,
  spaces: (q: Query) => ['spaces', q] as const,
  space: (id: number, q: Query) => ['space', id, q] as const,
  myBookings: (q: Query) => ['bookings', 'my', q] as const,
  booking: (id: number) => ['booking', id] as const,
  history: (id: number) => ['history', id] as const,
  adminBookings: (q: Query) => ['admin', 'bookings', q] as const,
  stats: (date?: string) => ['admin', 'stats', date ?? 'today'] as const,
  audit: (q: Query) => ['admin', 'audit', q] as const,
  notifications: (q: Query) => ['notifications', q] as const,
  unread: ['notifications', 'unread'] as const,
  suggestions: (id: number, q: Query) => ['suggestions', id, q] as const,
};

// Справочники меняются редко — держим их свежими 10 минут.
const DICTIONARY = { staleTime: 10 * 60_000 };

export const useMe = (enabled: boolean) => useQuery({ queryKey: keys.me, queryFn: () => api<User>('/users/me'), enabled });
export const useCategories = () => useQuery({ queryKey: keys.categories, queryFn: () => api<Category[]>('/categories'), ...DICTIONARY });
export const useLocations = () => useQuery({ queryKey: keys.locations, queryFn: () => api<Location[]>('/locations'), ...DICTIONARY });
export const useFeatures = () => useQuery({ queryKey: keys.features, queryFn: () => api<Feature[]>('/features'), ...DICTIONARY });

const PAGE = 24;

/** Бесконечный список: следующая страница подгружается, когда пользователь докрутил до конца. */
function useInfinite<T>(key: readonly unknown[], path: string, query: Query, enabled = true) {
  return useInfiniteQuery({
    queryKey: key,
    queryFn: ({ pageParam, signal }) => api<Paged<T>>(path, { query: { ...query, page: pageParam, pageSize: query.pageSize ?? PAGE }, signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export const useResources = (q: Query, enabled = true) => useInfinite<Resource>(keys.resources(q), '/resources', q, enabled);
export const useAvailable = (q: Query, enabled = true) => useInfinite<AvailableResource>(keys.available(q), '/resources/available', q, enabled);
export const useResource = (id: number) => useQuery({ queryKey: keys.resource(id), queryFn: () => api<Resource>(`/resources/${id}`) });

export const useSchedule = (id: number, from: string, to: string, enabled = true) =>
  useQuery({
    queryKey: keys.schedule(id, from, to),
    queryFn: () => api<ResourceSchedule>(`/resources/${id}/schedule`, { query: { from, to } }),
    placeholderData: keepPreviousData,
    enabled,
  });

export const useSpaces = (q: Query, enabled = true) =>
  useQuery({ queryKey: keys.spaces(q), queryFn: () => api<Space[]>('/spaces', { query: q }), placeholderData: keepPreviousData, enabled });

export const useSpace = (id: number, q: Query) =>
  useQuery({ queryKey: keys.space(id, q), queryFn: () => api<Space>(`/spaces/${id}`, { query: q }), placeholderData: keepPreviousData });

export const useMyBookings = (q: Query, enabled = true) => useInfinite<Booking>(keys.myBookings(q), '/bookings/my', q, enabled);
export const useBooking = (id: number) => useQuery({ queryKey: keys.booking(id), queryFn: () => api<Booking>(`/bookings/${id}`) });
export const useHistory = (id: number) => useQuery({ queryKey: keys.history(id), queryFn: () => api<AuditLog[]>(`/bookings/${id}/history`) });

export const useAdminBookings = (q: Query, enabled = true) => useInfinite<Booking>(keys.adminBookings(q), '/admin/bookings', q, enabled);
export const useStats = (date?: string) => useQuery({ queryKey: keys.stats(date), queryFn: () => api<AdminStats>('/admin/stats', { query: { date } }) });
export const useAudit = (q: Query) => useQuery({ queryKey: keys.audit(q), queryFn: () => api<Paged<AuditLog>>('/admin/audit', { query: q }) });

export const useNotifications = (q: Query, enabled = true) => useInfinite<Notification>(keys.notifications(q), '/notifications', q, enabled);
export const useUnread = (enabled: boolean) =>
  useQuery({ queryKey: keys.unread, queryFn: () => api<{ count: number }>('/notifications/unread-count'), enabled, select: (d) => d.count });

export const useSuggestions = (id: number, q: Query, enabled: boolean) =>
  useQuery({ queryKey: keys.suggestions(id, q), queryFn: () => api<Suggestion[]>(`/resources/${id}/suggestions`, { query: q }), enabled });

/** После любого изменения брони: всё, что могло показывать её или занятость, перечитывается. */
export function invalidateBookings(qc: QueryClient) {
  return Promise.all([
    qc.invalidateQueries({ queryKey: ['bookings'] }),
    qc.invalidateQueries({ queryKey: ['booking'] }),
    qc.invalidateQueries({ queryKey: ['history'] }),
    qc.invalidateQueries({ queryKey: ['schedule'] }),
    qc.invalidateQueries({ queryKey: ['available'] }),
    qc.invalidateQueries({ queryKey: ['spaces'] }),
    qc.invalidateQueries({ queryKey: ['space'] }),
    qc.invalidateQueries({ queryKey: ['admin'] }),
    qc.invalidateQueries({ queryKey: ['notifications'] }),
  ]);
}
