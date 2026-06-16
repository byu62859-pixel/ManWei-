import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { useAuthStore } from './stores/authStore';
import { Login } from './pages/Login';
import { Register } from './pages/Register';
import { Home } from './pages/Home';
import { AnimeDetail } from './pages/AnimeDetail';
import { Favorites } from './pages/Favorites';
import { Reviews } from './pages/Reviews';
import { DataCenter } from './pages/DataCenter';
import { Profile } from './pages/Profile';
import { AppShell } from './components/AppShell';

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const isLoggedIn = useAuthStore((s) => s.isLoggedIn);
  return isLoggedIn ? <>{children}</> : <Navigate to="/login" />;
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route
          element={
            <ProtectedRoute>
              <AppShell />
            </ProtectedRoute>
          }
        >
          <Route path="/anime/:id" element={<AnimeDetail />} />
          <Route path="/favorites" element={<Favorites />} />
          <Route path="/reviews" element={<Reviews />} />
          <Route path="/data" element={<DataCenter />} />
          <Route path="/profile" element={<Profile />} />
          <Route path="/" element={<Home />} />
        </Route>
        <Route
          path="*"
          element={
            <Navigate to="/" />
          }
        />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
