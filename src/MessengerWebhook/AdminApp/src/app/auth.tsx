import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { api } from "../lib/api";
import type { AdminUser, AuthState } from "../lib/types";

type AuthContextValue = {
  loading: boolean;
  authenticated: boolean;
  csrfToken: string;
  user?: AdminUser | null;
  refresh: () => Promise<void>;
  login: (email: string, password: string, rememberMe: boolean) => Promise<void>;
  logout: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>({ authenticated: false, antiForgeryToken: "" });
  const [loading, setLoading] = useState(true);

  const refresh = async () => {
    const nextState = await api.getAuthState();
    setState(nextState);
  };

  useEffect(() => {
    refresh().finally(() => setLoading(false));
  }, []);

  const value = useMemo<AuthContextValue>(() => ({
    loading,
    authenticated: state.authenticated,
    csrfToken: state.antiForgeryToken,
    user: state.user,
    refresh,
    login: async (email, password, rememberMe) => {
      await api.login(email, password, rememberMe, state.antiForgeryToken);
      await refresh();
    },
    logout: async () => {
      await api.logout(state.antiForgeryToken);
      await refresh();
    }
  }), [loading, state]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used inside AuthProvider");
  }

  return context;
}
