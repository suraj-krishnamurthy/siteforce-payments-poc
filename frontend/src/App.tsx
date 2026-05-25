import { Routes, Route, Navigate } from 'react-router-dom'
import AppShell from './layout/AppShell'
import UploadPage from './pages/UploadPage'
import DashboardPage from './pages/DashboardPage'
import RulesPage from './pages/RulesPage'
import AuditPage from './pages/AuditPage'

function App() {
  return (
    <AppShell>
      <Routes>
        <Route path="/" element={<Navigate to="/upload" replace />} />
        <Route path="/upload" element={<UploadPage />} />
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/rules" element={<RulesPage />} />
        <Route path="/audit" element={<AuditPage />} />
      </Routes>
    </AppShell>
  )
}

export default App
