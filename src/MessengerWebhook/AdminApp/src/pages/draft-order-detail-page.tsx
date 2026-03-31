import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useParams } from "react-router-dom";
import { useAuth } from "../app/auth";
import { StatusPill } from "../components/status-pill";
import { api } from "../lib/api";
import { formatDate, formatMoney } from "../lib/format";

export function DraftOrderDetailPage() {
  const { id = "" } = useParams();
  const { csrfToken } = useAuth();
  const queryClient = useQueryClient();
  const [notes, setNotes] = useState("");
  const detailQuery = useQuery({
    queryKey: ["draft-order", id],
    queryFn: () => api.getDraftOrder(id)
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
      await queryClient.invalidateQueries({ queryKey: ["draft-order", id] });
    }
  });

  const rejectMutation = useMutation({
    mutationFn: () => api.rejectDraft(id, notes, csrfToken),
    onSuccess: async () => {
      setNotes("");
      await queryClient.invalidateQueries({ queryKey: ["draft-order", id] });
    }
  });

  const draft = detailQuery.data;

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <span className="eyebrow">Draft detail</span>
          <h1>{draft?.draftCode ?? "Đơn nháp"}</h1>
        </div>
      </div>

      {detailQuery.isLoading ? <div className="card">Đang tải chi tiết đơn...</div> : null}
      {detailQuery.isError ? <div className="error-box">{(detailQuery.error as Error).message}</div> : null}

      {draft ? (
        <div className="detail-grid">
          <div className="card stack">
            <div className="detail-row"><span>Trạng thái</span><StatusPill value={draft.status} /></div>
            <div className="detail-row"><span>Risk</span><StatusPill value={draft.riskLevel} /></div>
            <div className="detail-row"><span>SĐT</span><strong>{draft.customerPhone}</strong></div>
            <div className="detail-row"><span>Địa chỉ</span><strong>{draft.shippingAddress}</strong></div>
            <div className="detail-row"><span>Tổng tiền</span><strong>{formatMoney(draft.grandTotal)}</strong></div>
            <div className="detail-row"><span>Lỗi submit</span><strong>{draft.lastSubmissionError ?? "N/A"}</strong></div>
            <div className="button-row">
              <button className="primary-button" onClick={() => approveMutation.mutate()} type="button">Approve & submit</button>
              <button className="secondary-button" onClick={() => retryMutation.mutate()} type="button">Retry submit</button>
            </div>
            <textarea className="text-area" onChange={(event) => setNotes(event.target.value)} placeholder="Ghi chú từ chối" value={notes} />
            <button className="ghost-button" onClick={() => rejectMutation.mutate()} type="button">Reject draft</button>
            {approveMutation.data ? <div className="success-box">{approveMutation.data.message}</div> : null}
            {retryMutation.data ? <div className="success-box">{retryMutation.data.message}</div> : null}
            {rejectMutation.data ? <div className="success-box">{rejectMutation.data.message}</div> : null}
          </div>

          <div className="card stack">
            <h2>Sản phẩm</h2>
            {draft.items.map((item) => (
              <article className="list-item" key={item.id}>
                <div>
                  <strong>{item.productName}</strong>
                  <div>{item.productCode}</div>
                  <div>{item.giftName ? `Quà: ${item.giftName}` : "Không có quà"}</div>
                </div>
                <div>
                  <div>x{item.quantity}</div>
                  <strong>{formatMoney(item.unitPrice)}</strong>
                </div>
              </article>
            ))}
          </div>

          <div className="card stack">
            <h2>Audit log</h2>
            {draft.auditLogs.map((log) => (
              <article className="list-item" key={log.id}>
                <div>
                  <strong>{log.action}</strong>
                  <div>{log.actorEmail}</div>
                  <div>{log.details ?? "Không có chi tiết"}</div>
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
