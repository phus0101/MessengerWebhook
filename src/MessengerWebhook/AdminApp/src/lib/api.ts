import type {
  AuthState,
  CommandResult,
  CustomerOption,
  DashboardOverview,
  DraftOrderDetail,
  DraftOrderListItem,
  NobitaProductOption,
  ProductMapping,
  SupportCaseDetail,
  SupportCaseListItem,
  UpdateDraftOrderInput
} from "./types";

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const payload = (await response.json().catch(() => null)) as { error?: string } | null;
    throw new Error(payload?.error ?? `Request failed with ${response.status}`);
  }

  return (await response.json()) as T;
}

async function postJson<T>(url: string, csrfToken: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method: "POST",
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": csrfToken
    },
    body: body === undefined ? undefined : JSON.stringify(body)
  });

  return readJson<T>(response);
}

export const api = {
  getAuthState() {
    return fetch("/admin/api/auth/me", { credentials: "include" }).then((response) => readJson<AuthState>(response));
  },
  login(email: string, password: string, rememberMe: boolean, csrfToken: string) {
    return postJson<{ success: boolean }>("/admin/api/auth/login", csrfToken, { email, password, rememberMe });
  },
  logout(csrfToken: string) {
    return postJson<{ success: boolean }>("/admin/api/auth/logout", csrfToken);
  },
  getDashboard() {
    return fetch("/admin/api/dashboard", { credentials: "include" }).then((response) => readJson<DashboardOverview>(response));
  },
  getDraftOrders() {
    return fetch("/admin/api/draft-orders", { credentials: "include" }).then((response) => readJson<DraftOrderListItem[]>(response));
  },
  getDraftOrder(id: string) {
    return fetch(`/admin/api/draft-orders/${id}`, { credentials: "include" }).then((response) => readJson<DraftOrderDetail>(response));
  },
  searchCustomers(query: string) {
    const normalizedQuery = query.trim();
    if (!normalizedQuery) {
      return Promise.resolve([] as CustomerOption[]);
    }

    return fetch(`/admin/api/customers?query=${encodeURIComponent(normalizedQuery)}`, { credentials: "include" }).then((response) => readJson<CustomerOption[]>(response));
  },
  updateDraft(id: string, payload: UpdateDraftOrderInput, csrfToken: string) {
    return postJson<CommandResult>(`/admin/api/draft-orders/${id}/update`, csrfToken, payload);
  },
  approveSubmit(id: string, csrfToken: string) {
    return postJson<CommandResult>(`/admin/api/draft-orders/${id}/approve-submit`, csrfToken);
  },
  rejectDraft(id: string, notes: string, csrfToken: string) {
    return postJson<CommandResult>(`/admin/api/draft-orders/${id}/reject`, csrfToken, { notes });
  },
  retrySubmit(id: string, csrfToken: string) {
    return postJson<CommandResult>(`/admin/api/draft-orders/${id}/retry-submit`, csrfToken);
  },
  getSupportCases() {
    return fetch("/admin/api/support-cases", { credentials: "include" }).then((response) => readJson<SupportCaseListItem[]>(response));
  },
  getSupportCase(id: string) {
    return fetch(`/admin/api/support-cases/${id}`, { credentials: "include" }).then((response) => readJson<SupportCaseDetail>(response));
  },
  claimSupportCase(id: string, csrfToken: string) {
    return postJson<{ success: boolean }>(`/admin/api/support-cases/${id}/claim`, csrfToken);
  },
  resolveSupportCase(id: string, notes: string, csrfToken: string) {
    return postJson<{ success: boolean }>(`/admin/api/support-cases/${id}/resolve`, csrfToken, { notes });
  },
  cancelSupportCase(id: string, notes: string, csrfToken: string) {
    return postJson<{ success: boolean }>(`/admin/api/support-cases/${id}/cancel`, csrfToken, { notes });
  },
  getProductMappings(search?: string) {
    const query = search ? `?search=${encodeURIComponent(search)}` : "";
    return fetch(`/admin/api/product-mappings${query}`, { credentials: "include" }).then((response) => readJson<ProductMapping[]>(response));
  },
  updateProductMapping(id: string, nobitaProductId: number, nobitaWeight: number, csrfToken: string) {
    return postJson<CommandResult>(`/admin/api/product-mappings/${id}`, csrfToken, { nobitaProductId, nobitaWeight });
  },
  searchNobitaProducts(search?: string) {
    const query = search ? `?search=${encodeURIComponent(search)}` : "";
    return fetch(`/admin/api/nobita/products${query}`, { credentials: "include" }).then((response) => readJson<NobitaProductOption[]>(response));
  },
  syncNobitaProducts(search: string, csrfToken: string) {
    return postJson<ProductMapping[]>(`/admin/api/nobita/products/sync`, csrfToken, { search });
  }
};
