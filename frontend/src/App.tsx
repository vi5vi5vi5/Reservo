import { Link, Route, Routes } from 'react-router';
import { Compass } from 'lucide-react';
import { Shell } from './app/Shell';
import { Empty } from './ui/controls';
import { AdminPage } from './features/admin/AdminPage';
import { LoginPage, RegisterPage } from './features/account/AuthPages';
import { NotificationsPage } from './features/account/NotificationsPage';
import { ProfilePage } from './features/account/ProfilePage';
import { BookingPage } from './features/bookings/BookingPage';
import { MyBookingsPage } from './features/bookings/MyBookingsPage';
import { CatalogPage } from './features/catalog/CatalogPage';
import { ResourcePage } from './features/resource/ResourcePage';
import { HomePage } from './features/search/HomePage';
import { ResultsPage } from './features/search/ResultsPage';
import { SpacePage } from './features/space/SpacePage';

export function App() {
  return (
    <Shell>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/search" element={<ResultsPage />} />
        <Route path="/catalog" element={<CatalogPage />} />
        <Route path="/resources/:id" element={<ResourcePage />} />
        <Route path="/spaces/:id" element={<SpacePage />} />
        <Route path="/bookings" element={<MyBookingsPage />} />
        <Route path="/bookings/:id" element={<BookingPage />} />
        <Route path="/notifications" element={<NotificationsPage />} />
        <Route path="/profile" element={<ProfilePage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/admin" element={<AdminPage />} />
        <Route path="/admin/:tab" element={<AdminPage />} />
        <Route path="*" element={<NotFound />} />
      </Routes>
    </Shell>
  );
}

function NotFound() {
  return (
    <div className="page" style={{ justifyContent: 'center' }}>
      <Empty icon={Compass} title="Такой страницы нет" text="Возможно, ссылка устарела или в ней опечатка.">
        <Link to="/" className="rv-btn rv-btn-primary rv-btn-lg">На главную</Link>
      </Empty>
    </div>
  );
}
