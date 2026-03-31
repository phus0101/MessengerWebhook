import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { useParams } from "react-router-dom";
import { useAuth } from "../app/auth";
import { StatusPill } from "../components/status-pill";
import { api } from "../lib/api";
import { formatDate, formatMoney } from "../lib/format";
import type { CommandResult, CustomerOption, DraftOrderDetail, DraftProductOption, UpdateDraftOrderInput, UpdateDraftOrderItemInput } from "../lib/types";

type DraftEditState = {
  customerIdentityId?: string | null;
  customerName: string;
  customerPhone: string;
  shippingAddress: string;
  items: UpdateDraftOrderItemInput[];
};

function buildEditState(draft: DraftOrderDetail): DraftEditState {
  return {
    customerIdentityId: null,
    customerName: draft.customerName ?? "",
    customerPhone: draft.customerPhone,
    shippingAddress: draft.shippingAddress,
    items: draft.items.map((item) => ({
      productCode: item.productCode,
      quantity: item.quantity,
      giftCode: item.giftCode ?? ""
    }))
  };
}

function buildDefaultItem(availableProducts: DraftProductOption[]): UpdateDraftOrderItemInput {
  const [firstProduct] = availableProducts;
  if (!firstProduct) {
    return { productCode: "", quantity: 1, giftCode: "" };
  }

  return {
    productCode: firstProduct.code,
    quantity: 1,
    giftCode: firstProduct.giftOptions[0]?.code ?? ""
  };
}

function normalizeGiftCode(value: string | null | undefined) {
  const normalized = value?.trim() ?? "";
  return normalized.length === 0 ? null : normalized;
}

function formatCustomerSummary(customer: CustomerOption) {
  const name = customer.fullName?.trim() || "Khach chua co ten";
  const phone = customer.phoneNumber?.trim() || "Chua co SĐT";
  return `${name} - ${phone}`;
}

export function DraftOrderDetailPage() {
  const { id = "" } = useParams();
  const { csrfToken } = useAuth();
  const queryClient = useQueryClient();
  const [notes, setNotes] = useState("");
  const [formState, setFormState] = useState<DraftEditState | null>(null);
  const [customerSearchTerm, setCustomerSearchTerm] = useState("");
  const [selectedCustomer, setSelectedCustomer] = useState<CustomerOption | null>(null);

  const detailQuery = useQuery({
    queryKey: ["draft-order", id],
    queryFn: () => api.getDraftOrder(id)
  });

  const trimmedCustomerSearchTerm = customerSearchTerm.trim();
  const customerSearchQuery = useQuery({
    queryKey: ["customer-search", trimmedCustomerSearchTerm],
    queryFn: () => api.searchCustomers(trimmedCustomerSearchTerm),
    enabled: Boolean(detailQuery.data?.isEditable) && trimmedCustomerSearchTerm.length >= 2
  });

  useEffect(() => {
    if (detailQuery.data) {
      setFormState(buildEditState(detailQuery.data));
      setSelectedCustomer(null);
      setCustomerSearchTerm("");
    }
  }, [detailQuery.data]);

  const updateMutation = useMutation({
    mutationFn: (payload: UpdateDraftOrderInput) => api.updateDraft(id, payload, csrfToken),
    onSuccess: async (result) => {
      if (!result.succeeded) {
        return;
      }

      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["draft-orders"] }),
        queryClient.invalidateQueries({ queryKey: ["draft-order", id] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard"] })
      ]);
    }
  });

  const approveMutation = useMutation({
    mutationFn: () => api.approveSubmit(id, csrfToken),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["draft-orders"] }),
        queryClient.invalidateQueries({ queryKey: ["draft-order", id] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard"] })
      ]);
    }
  });

  const retryMutation = useMutation({
    mutationFn: () => api.retrySubmit(id, csrfToken),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["draft-orders"] }),
        queryClient.invalidateQueries({ queryKey: ["draft-order", id] })
      ]);
    }
  });

  const rejectMutation = useMutation({
    mutationFn: () => api.rejectDraft(id, notes, csrfToken),
    onSuccess: async () => {
      setNotes("");
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["draft-orders"] }),
        queryClient.invalidateQueries({ queryKey: ["draft-order", id] }),
        queryClient.invalidateQueries({ queryKey: ["dashboard"] })
      ]);
    }
  });

  const draft = detailQuery.data;
  const availableProducts = useMemo(() => {
    if (!draft) {
      return [] as DraftProductOption[];
    }

    const merged = new Map<string, DraftProductOption>();
    draft.availableProducts.forEach((product) => {
      merged.set(product.code, product);
    });

    draft.items.forEach((item) => {
      if (merged.has(item.productCode)) {
        return;
      }

      merged.set(item.productCode, {
        code: item.productCode,
        name: item.productName,
        unitPrice: item.unitPrice,
        giftOptions: item.giftCode && item.giftName
          ? [{ code: item.giftCode, name: item.giftName }]
          : []
      });
    });

    return Array.from(merged.values());
  }, [draft]);
  const isActionLocked = !draft?.isEditable;

  const catalogByCode = useMemo(() => {
    return new Map(availableProducts.map((product) => [product.code, product]));
  }, [availableProducts]);

  const previewTotal = useMemo(() => {
    if (!formState) {
      return draft?.grandTotal ?? 0;
    }

    const merchandise = formState.items.reduce((sum, item) => {
      const product = catalogByCode.get(item.productCode);
      return sum + (product?.unitPrice ?? 0) * item.quantity;
    }, 0);

    const expandedCount = formState.items.reduce((sum, item) => sum + Math.max(item.quantity, 0), 0);
    const hasCombo = formState.items.some((item) => item.productCode === "COMBO_2");
    const shippingFee = expandedCount >= 2 || hasCombo ? 0 : 30000;
    return merchandise + shippingFee;
  }, [catalogByCode, draft?.grandTotal, formState]);

  const handleProductChange = (index: number, productCode: string) => {
    setFormState((current) => {
      if (!current) return current;
      const currentItem = current.items[index];
      const product = catalogByCode.get(productCode);
      const nextItems = current.items.map((item, itemIndex) =>
        itemIndex === index
          ? {
              productCode,
              quantity: currentItem?.quantity ?? 1,
              giftCode: product?.giftOptions[0]?.code ?? ""
            }
          : item);

      return { ...current, items: nextItems };
    });
  };

  const handleQuantityChange = (index: number, quantityValue: string) => {
    const quantity = Number(quantityValue);
    setFormState((current) => {
      if (!current) return current;
      const currentItem = current.items[index];
      const nextItems = current.items.map((item, itemIndex) =>
        itemIndex === index
          ? {
              productCode: currentItem?.productCode ?? "",
              quantity: Number.isFinite(quantity) && quantity > 0 ? quantity : 1,
              giftCode: currentItem?.giftCode ?? ""
            }
          : item);

      return { ...current, items: nextItems };
    });
  };

  const handleGiftChange = (index: number, giftCode: string) => {
    setFormState((current) => {
      if (!current) return current;
      const currentItem = current.items[index];
      const nextItems = current.items.map((item, itemIndex) =>
        itemIndex === index
          ? {
              productCode: currentItem?.productCode ?? "",
              quantity: currentItem?.quantity ?? 1,
              giftCode
            }
          : item);

      return { ...current, items: nextItems };
    });
  };

  const addItem = () => {
    setFormState((current) => {
      if (!current) return current;
      return {
        ...current,
        items: [...current.items, buildDefaultItem(availableProducts)]
      };
    });
  };

  const removeItem = (index: number) => {
    setFormState((current) => {
      if (!current) return current;
      return {
        ...current,
        items: current.items.filter((_, itemIndex) => itemIndex !== index)
      };
    });
  };

  const handleSave = () => {
    if (!formState) {
      return;
    }

    updateMutation.mutate({
      customerIdentityId: formState.customerIdentityId ?? null,
      customerName: formState.customerName.trim() || null,
      customerPhone: formState.customerPhone,
      shippingAddress: formState.shippingAddress,
      items: formState.items.map((item) => ({
        productCode: item.productCode,
        quantity: item.quantity,
        giftCode: normalizeGiftCode(item.giftCode)
      }))
    });
  };

  const handleSelectCustomer = (customer: CustomerOption) => {
    setSelectedCustomer(customer);
    setCustomerSearchTerm("");
    setFormState((current) => current ? {
      ...current,
      customerIdentityId: customer.customerIdentityId,
      customerName: customer.fullName?.trim() || current.customerName,
      customerPhone: customer.phoneNumber?.trim() || current.customerPhone,
      shippingAddress: customer.shippingAddress?.trim() || current.shippingAddress
    } : current);
  };

  const clearSelectedCustomer = () => {
    setSelectedCustomer(null);
    setFormState((current) => current ? {
      ...current,
      customerIdentityId: null
    } : current);
  };

  const feedback = updateMutation.data ?? approveMutation.data ?? retryMutation.data ?? rejectMutation.data ?? null;
  const feedbackClassName = feedback?.succeeded === false ? "error-box" : "success-box";

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <span className="eyebrow">Draft detail</span>
          <h1>{draft?.draftCode ?? "Don nhap"}</h1>
        </div>
      </div>

      {detailQuery.isLoading ? <div className="card">Dang tai chi tiet don...</div> : null}
      {detailQuery.isError ? <div className="error-box">{(detailQuery.error as Error).message}</div> : null}

      {draft && formState ? (
        <div className="detail-grid">
          <div className="card stack">
            <div className="detail-row"><span>Trang thai</span><StatusPill kind="draft-status" value={draft.status} /></div>
            <div className="detail-row"><span>Risk</span><StatusPill kind="risk-level" value={draft.riskLevel} /></div>
            <div className="detail-row"><span>Tong du kien</span><strong>{formatMoney(previewTotal)}</strong></div>
            {!draft.isEditable ? <div className="error-box">Don da gui sang Nobita nen form chi con che do xem.</div> : null}

            <div className="stack">
              <label>
                <span>Tim khach hang co san</span>
                <input
                  disabled={!draft.isEditable}
                  onChange={(event) => setCustomerSearchTerm(event.target.value)}
                  placeholder="Nhap SĐT, ten hoac dia chi"
                  value={customerSearchTerm}
                />
              </label>

              {selectedCustomer ? (
                <div className="card stack">
                  <div className="detail-row">
                    <strong>{formatCustomerSummary(selectedCustomer)}</strong>
                    <button className="ghost-button" disabled={!draft.isEditable} onClick={clearSelectedCustomer} type="button">
                      Bo chon
                    </button>
                  </div>
                  <div>{selectedCustomer.shippingAddress ?? "Chua co dia chi"}</div>
                  <div>Lich su: {selectedCustomer.totalOrders} don, giao thanh cong {selectedCustomer.successfulDeliveries}, bom/huy {selectedCustomer.failedDeliveries}</div>
                  <div>Da nap thong tin vao form. Ban van co the chinh sua truoc khi luu.</div>
                </div>
              ) : null}

              {draft.isEditable && trimmedCustomerSearchTerm.length >= 2 ? (
                <div className="stack">
                  {customerSearchQuery.isLoading ? <div>Dang tim khach hang...</div> : null}
                  {customerSearchQuery.isError ? <div className="error-box">{(customerSearchQuery.error as Error).message}</div> : null}
                  {customerSearchQuery.data?.length === 0 && !customerSearchQuery.isLoading ? (
                    <div className="card">Khong tim thay khach hang phu hop.</div>
                  ) : null}
                  {customerSearchQuery.data?.map((customer) => (
                    <article className="list-item" key={customer.customerIdentityId}>
                      <div>
                        <strong>{formatCustomerSummary(customer)}</strong>
                        <div>{customer.shippingAddress ?? "Chua co dia chi"}</div>
                        <div>{customer.totalOrders} don - giao thanh cong {customer.successfulDeliveries} - bom/huy {customer.failedDeliveries}</div>
                      </div>
                      <button className="secondary-button" onClick={() => handleSelectCustomer(customer)} type="button">
                        Chon khach nay
                      </button>
                    </article>
                  ))}
                </div>
              ) : null}
            </div>

            <label>
              <span>Ten khach</span>
              <input
                disabled={!draft.isEditable}
                onChange={(event) => setFormState((current) => current ? { ...current, customerName: event.target.value } : current)}
                value={formState.customerName}
              />
            </label>

            <label>
              <span>So dien thoai</span>
              <input
                disabled={!draft.isEditable}
                onChange={(event) => setFormState((current) => current ? { ...current, customerPhone: event.target.value } : current)}
                value={formState.customerPhone}
              />
            </label>

            <label>
              <span>Dia chi giao hang</span>
              <textarea
                className="text-area"
                disabled={!draft.isEditable}
                onChange={(event) => setFormState((current) => current ? { ...current, shippingAddress: event.target.value } : current)}
                value={formState.shippingAddress}
              />
            </label>

            <div className="button-row">
              <button className="primary-button" disabled={!draft.isEditable || updateMutation.isPending} onClick={handleSave} type="button">
                Luu thay doi
              </button>
              <button className="primary-button" disabled={isActionLocked || approveMutation.isPending} onClick={() => approveMutation.mutate()} type="button">
                Approve & submit
              </button>
              <button className="secondary-button" disabled={isActionLocked || retryMutation.isPending} onClick={() => retryMutation.mutate()} type="button">
                Retry submit
              </button>
            </div>

            <textarea className="text-area" onChange={(event) => setNotes(event.target.value)} placeholder="Ghi chu tu choi" value={notes} />
            <button className="ghost-button" disabled={isActionLocked || rejectMutation.isPending} onClick={() => rejectMutation.mutate()} type="button">Reject draft</button>

            {feedback ? <div className={feedbackClassName}>{feedback.message}</div> : null}
          </div>

          <div className="card stack">
            <div className="detail-row">
              <h2>San pham</h2>
              <button className="secondary-button" disabled={!draft.isEditable} onClick={addItem} type="button">
                Them dong
              </button>
            </div>

            {formState.items.map((item, index) => {
              const product = catalogByCode.get(item.productCode);
              const giftOptions = product?.giftOptions ?? [];

              return (
                <article className="draft-item-editor" key={`${item.productCode}-${index}`}>
                  <label>
                    <span>San pham</span>
                    <select
                      disabled={!draft.isEditable}
                      onChange={(event) => handleProductChange(index, event.target.value)}
                      value={item.productCode}
                    >
                      {availableProducts.map((option) => (
                        <option key={option.code} value={option.code}>
                          {option.name} ({option.code})
                        </option>
                      ))}
                    </select>
                  </label>

                  <label>
                    <span>So luong</span>
                    <input
                      disabled={!draft.isEditable}
                      min={1}
                      onChange={(event) => handleQuantityChange(index, event.target.value)}
                      type="number"
                      value={item.quantity}
                    />
                  </label>

                  <label>
                    <span>Qua tang</span>
                    <select
                      disabled={!draft.isEditable}
                      onChange={(event) => handleGiftChange(index, event.target.value)}
                      value={item.giftCode ?? ""}
                    >
                      <option value="">Khong chon qua</option>
                      {giftOptions.map((gift) => (
                        <option key={gift.code} value={gift.code}>
                          {gift.name}
                        </option>
                      ))}
                    </select>
                  </label>

                  <div className="draft-item-editor__meta">
                    <span>{formatMoney((product?.unitPrice ?? 0) * item.quantity)}</span>
                    <button className="ghost-button" disabled={!draft.isEditable || formState.items.length === 1} onClick={() => removeItem(index)} type="button">
                      Xoa dong
                    </button>
                  </div>
                </article>
              );
            })}
          </div>

          <div className="card stack">
            <h2>Audit log</h2>
            {draft.auditLogs.map((log) => (
              <article className="list-item" key={log.id}>
                <div>
                  <strong>{log.action}</strong>
                  <div>{log.actorEmail}</div>
                  <div>{log.details ?? "Khong co chi tiet"}</div>
                </div>
                <span>{formatDate(log.createdAt)}</span>
              </article>
            ))}
          </div>
        </div>
      ) : null}
    </section>
  );
}
