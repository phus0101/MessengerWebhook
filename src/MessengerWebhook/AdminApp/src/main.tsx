import React from "react";
import ReactDOM from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider, useAuth } from "./app/auth";
import { AdminLayout } from "./components/layout";
import { DashboardPage } from "./pages/dashboard-page";
import { DraftOrderDetailPage } from "./pages/draft-order-detail-page";
import { DraftOrdersPage } from "./pages/draft-orders-page";
import { LoginPage } from "./pages/login-page";
import { ProductMappingsPage } from "./pages/product-mappings-page";
import { SupportCaseDetailPage } from "./pages/support-case-detail-page";
import { SupportCasesPage } from "./pages/support-cases-page";
import { VectorSearchPage } from "./pages/vector-search-page";
import { ABTestDashboard } from "./pages/metrics/ab-test-dashboard";
import "./styles.css";

const queryClient = new QueryClient();

function ProtectedRoutes() {
  const { authenticated, loading } = useAuth();

  if (loading) {
    return <div className="screen-center">Đang tải admin...</div>;
  }

  if (!authenticated) {
    return <Navigate replace to="/login" />;
  }

  return <AdminLayout />;
}

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <BrowserRouter basename="/admin">
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route element={<ProtectedRoutes />}>
              <Route path="/" element={<DashboardPage />} />
              <Route path="/draft-orders" element={<DraftOrdersPage />} />
              <Route path="/draft-orders/:id" element={<DraftOrderDetailPage />} />
              <Route path="/support-cases" element={<SupportCasesPage />} />
              <Route path="/support-cases/:id" element={<SupportCaseDetailPage />} />
              <Route path="/product-mappings" element={<ProductMappingsPage />} />
              <Route path="/vector-search" element={<VectorSearchPage />} />
              <Route path="/metrics" element={<ABTestDashboard />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
  </React.StrictMode>
);
