export type AdminUser = {
  managerId: string;
  email: string;
  fullName: string;
  tenantId: string;
  facebookPageId?: string | null;
};

export type AuthState = {
  authenticated: boolean;
  antiForgeryToken: string;
  user?: AdminUser | null;
};

export type DashboardOverview = {
  pendingDrafts: number;
  submitFailedDrafts: number;
  openSupportCases: number;
  claimedSupportCases: number;
};

export type DraftOrderListItem = {
  id: string;
  draftCode: string;
  facebookPageId?: string | null;
  customerName?: string | null;
  customerPhone: string;
  shippingAddress: string;
  status: string;
  riskLevel: string;
  requiresManualReview: boolean;
  assignedManagerEmail?: string | null;
  itemCount: number;
  grandTotal: number;
  createdAt: string;
};

export type DraftOrderItem = {
  id: string;
  productCode: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  giftCode?: string | null;
  giftName?: string | null;
};

export type AuditLog = {
  id: string;
  actorEmail: string;
  action: string;
  resourceType: string;
  resourceId: string;
  details?: string | null;
  createdAt: string;
};

export type DraftOrderDetail = {
  id: string;
  draftCode: string;
  facebookPageId?: string | null;
  customerName?: string | null;
  customerPhone: string;
  shippingAddress: string;
  status: string;
  riskLevel: string;
  riskSummary?: string | null;
  requiresManualReview: boolean;
  merchandiseTotal: number;
  shippingFee: number;
  grandTotal: number;
  assignedManagerEmail?: string | null;
  nobitaOrderId?: string | null;
  lastSubmissionError?: string | null;
  createdAt: string;
  reviewedAt?: string | null;
  reviewedByEmail?: string | null;
  submittedAt?: string | null;
  submittedByEmail?: string | null;
  items: DraftOrderItem[];
  auditLogs: AuditLog[];
};

export type SupportCaseListItem = {
  id: string;
  facebookPSID: string;
  facebookPageId?: string | null;
  reason: string;
  status: string;
  summary: string;
  assignedToEmail?: string | null;
  createdAt: string;
  claimedAt?: string | null;
  resolvedAt?: string | null;
};

export type SupportCaseDetail = {
  id: string;
  facebookPSID: string;
  facebookPageId?: string | null;
  reason: string;
  status: string;
  summary: string;
  transcriptExcerpt: string;
  assignedToEmail?: string | null;
  claimedByEmail?: string | null;
  resolvedByEmail?: string | null;
  resolutionNotes?: string | null;
  createdAt: string;
  claimedAt?: string | null;
  resolvedAt?: string | null;
  auditLogs: AuditLog[];
};

export type ProductMapping = {
  id: string;
  code: string;
  name: string;
  basePrice: number;
  nobitaProductId?: number | null;
  nobitaWeight: number;
  nobitaLastSyncedAt?: string | null;
  nobitaSyncError?: string | null;
};

export type NobitaProductOption = {
  productId: number;
  code: string;
  name: string;
  price: number;
  isOutOfStock: boolean;
};

export type CommandResult = {
  succeeded: boolean;
  message: string;
  externalReference?: string | null;
};
