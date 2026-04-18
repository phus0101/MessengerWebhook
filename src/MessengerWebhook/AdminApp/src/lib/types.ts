import type {
  DraftOrderStatus,
  RiskLevel,
  SupportCaseReason,
  SupportCaseStatus
} from "./admin-enums";

export type IndexingStatus = {
  jobId: string;
  status: "NotStarted" | "Running" | "Completed" | "Failed" | "Cancelled";
  totalProducts: number;
  indexedProducts: number;
  progressPercentage: number;
  currentProductId?: string | null;
  currentProductName?: string | null;
  startedAt: string;
  completedAt?: string | null;
  errorMessage?: string | null;
};

export type AdminUser = {
  managerId: string;
  email: string;
  fullName: string;
  tenantId: string;
  facebookPageId?: string | null;
  canAccessAllPagesInTenant?: boolean;
  visibilityMode?: string | null;
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
  status: DraftOrderStatus | number | null;
  riskLevel: RiskLevel | number | null;
  requiresManualReview: boolean;
  assignedManagerEmail?: string | null;
  itemCount: number;
  grandTotal: number;
  priceConfirmed: boolean;
  promotionConfirmed: boolean;
  shippingConfirmed: boolean;
  inventoryConfirmed: boolean;
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

export type GiftOption = {
  code: string;
  name: string;
};

export type DraftProductOption = {
  code: string;
  name: string;
  unitPrice: number;
  giftOptions: GiftOption[];
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

export type CustomerOption = {
  customerIdentityId: string;
  fullName?: string | null;
  phoneNumber?: string | null;
  shippingAddress?: string | null;
  totalOrders: number;
  successfulDeliveries: number;
  failedDeliveries: number;
  lastInteractionAt?: string | null;
};

export type DraftOrderDetail = {
  id: string;
  draftCode: string;
  facebookPageId?: string | null;
  customerIdentityId?: string | null;
  customerName?: string | null;
  customerPhone: string;
  shippingAddress: string;
  status: DraftOrderStatus | number | null;
  riskLevel: RiskLevel | number | null;
  riskSummary?: string | null;
  requiresManualReview: boolean;
  merchandiseTotal: number;
  shippingFee: number;
  grandTotal: number;
  priceConfirmed: boolean;
  promotionConfirmed: boolean;
  shippingConfirmed: boolean;
  inventoryConfirmed: boolean;
  assignedManagerEmail?: string | null;
  nobitaOrderId?: string | null;
  lastSubmissionError?: string | null;
  createdAt: string;
  reviewedAt?: string | null;
  reviewedByEmail?: string | null;
  submittedAt?: string | null;
  submittedByEmail?: string | null;
  isEditable: boolean;
  linkedCustomer?: CustomerOption | null;
  items: DraftOrderItem[];
  availableProducts: DraftProductOption[];
  auditLogs: AuditLog[];
};

export type SupportCaseListItem = {
  id: string;
  facebookPSID: string;
  facebookPageId?: string | null;
  reason: SupportCaseReason | number | null;
  status: SupportCaseStatus | number | null;
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
  reason: SupportCaseReason | number | null;
  status: SupportCaseStatus | number | null;
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

export type UpdateDraftOrderItemInput = {
  productCode: string;
  quantity: number;
  giftCode?: string | null;
};

export type UpdateDraftOrderInput = {
  customerIdentityId?: string | null;
  customerName?: string | null;
  customerPhone: string;
  shippingAddress: string;
  items: UpdateDraftOrderItemInput[];
};
