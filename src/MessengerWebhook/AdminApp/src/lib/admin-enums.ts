export type DraftOrderStatus =
  | "Draft"
  | "PendingReview"
  | "Approved"
  | "Rejected"
  | "SubmittedToNobita"
  | "SubmitFailed";

export type RiskLevel = "Low" | "Medium" | "High";

export type SupportCaseStatus = "Open" | "Claimed" | "Resolved" | "Cancelled";

export type SupportCaseReason =
  | "PolicyException"
  | "RefundRequest"
  | "CancellationRequest"
  | "PromptInjection"
  | "UnsupportedQuestion"
  | "ManualReview";

export type AdminEnumKind = "draft-status" | "risk-level" | "support-status" | "support-reason";

export type AdminEnumValue = string | number | null | undefined;

type EnumPresentation = {
  label: string;
  tone: string;
};

const enumMaps: Record<AdminEnumKind, Record<string, EnumPresentation>> = {
  "draft-status": {
    "0": { label: "Draft", tone: "draft" },
    "1": { label: "Pending review", tone: "pendingreview" },
    "2": { label: "Approved", tone: "approved" },
    "3": { label: "Rejected", tone: "rejected" },
    "4": { label: "Submitted to Nobita", tone: "submittedtonobita" },
    "5": { label: "Submit failed", tone: "submitfailed" },
    Draft: { label: "Draft", tone: "draft" },
    PendingReview: { label: "Pending review", tone: "pendingreview" },
    Approved: { label: "Approved", tone: "approved" },
    Rejected: { label: "Rejected", tone: "rejected" },
    SubmittedToNobita: { label: "Submitted to Nobita", tone: "submittedtonobita" },
    SubmitFailed: { label: "Submit failed", tone: "submitfailed" }
  },
  "risk-level": {
    "0": { label: "Low", tone: "low" },
    "1": { label: "Medium", tone: "medium" },
    "2": { label: "High", tone: "high" },
    Low: { label: "Low", tone: "low" },
    Medium: { label: "Medium", tone: "medium" },
    High: { label: "High", tone: "high" }
  },
  "support-status": {
    "0": { label: "Open", tone: "open" },
    "1": { label: "Claimed", tone: "claimed" },
    "2": { label: "Resolved", tone: "resolved" },
    "3": { label: "Cancelled", tone: "cancelled" },
    Open: { label: "Open", tone: "open" },
    Claimed: { label: "Claimed", tone: "claimed" },
    Resolved: { label: "Resolved", tone: "resolved" },
    Cancelled: { label: "Cancelled", tone: "cancelled" }
  },
  "support-reason": {
    "0": { label: "Policy exception", tone: "policyexception" },
    "1": { label: "Refund request", tone: "refundrequest" },
    "2": { label: "Cancellation request", tone: "cancellationrequest" },
    "3": { label: "Prompt injection", tone: "promptinjection" },
    "4": { label: "Unsupported question", tone: "unsupportedquestion" },
    "5": { label: "Manual review", tone: "manualreview" },
    PolicyException: { label: "Policy exception", tone: "policyexception" },
    RefundRequest: { label: "Refund request", tone: "refundrequest" },
    CancellationRequest: { label: "Cancellation request", tone: "cancellationrequest" },
    PromptInjection: { label: "Prompt injection", tone: "promptinjection" },
    UnsupportedQuestion: { label: "Unsupported question", tone: "unsupportedquestion" },
    ManualReview: { label: "Manual review", tone: "manualreview" }
  }
};

export function getAdminEnumPresentation(value: AdminEnumValue, kind: AdminEnumKind): EnumPresentation {
  if (value === null || value === undefined) {
    return { label: "Unknown", tone: "unknown" };
  }

  const normalizedValue = String(value).trim();
  if (normalizedValue.length === 0) {
    return { label: "Unknown", tone: "unknown" };
  }

  return enumMaps[kind][normalizedValue] ?? {
    label: normalizedValue,
    tone: normalizedValue.toLowerCase().replace(/[^a-z0-9]+/g, "")
  };
}
