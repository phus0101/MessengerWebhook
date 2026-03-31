import { type AdminEnumKind, type AdminEnumValue, getAdminEnumPresentation } from "../lib/admin-enums";

type StatusPillProps = {
  value: AdminEnumValue;
  kind: AdminEnumKind;
};

export function StatusPill({ value, kind }: StatusPillProps) {
  const presentation = getAdminEnumPresentation(value, kind);
  return <span className={`status-pill status-pill--${presentation.tone}`}>{presentation.label}</span>;
}
