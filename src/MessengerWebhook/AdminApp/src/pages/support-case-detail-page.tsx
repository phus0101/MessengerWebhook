import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useParams } from "react-router-dom";
import { useAuth } from "../app/auth";
import { StatusPill } from "../components/status-pill";
import { api } from "../lib/api";
import { formatDate } from "../lib/format";

export function SupportCaseDetailPage() {
  const { id = "" } = useParams();
  const { csrfToken } = useAuth();
  const queryClient = useQueryClient();
  const [notes, setNotes] = useState("");
  const detailQuery = useQuery({
    queryKey: ["support-case", id],
    queryFn: () => api.getSupportCase(id)
  });

  const claimMutation = useMutation({
    mutationFn: () => api.claimSupportCase(id, csrfToken),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["support-case", id] });
    }
  });
  const resolveMutation = useMutation({
    mutationFn: () => api.resolveSupportCase(id, notes, csrfToken),
    onSuccess: async () => {
      setNotes("");
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["support-cases"] }),
        queryClient.invalidateQueries({ queryKey: ["support-case", id] })
      ]);
    }
  });
  const cancelMutation = useMutation({
    mutationFn: () => api.cancelSupportCase(id, notes, csrfToken),
    onSuccess: async () => {
      setNotes("");
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["support-cases"] }),
        queryClient.invalidateQueries({ queryKey: ["support-case", id] })
      ]);
    }
  });

  const supportCase = detailQuery.data;

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <span className="eyebrow">Support case</span>
          <h1>{supportCase ? supportCase.id.slice(0, 8) : "Case detail"}</h1>
        </div>
      </div>

      {detailQuery.isLoading ? <div className="card">Đang tải case...</div> : null}
      {detailQuery.isError ? <div className="error-box">{(detailQuery.error as Error).message}</div> : null}

      {supportCase ? (
        <div className="detail-grid">
          <div className="card stack">
            <div className="detail-row"><span>Trạng thái</span><StatusPill value={supportCase.status} /></div>
            <div className="detail-row"><span>Lý do</span><strong>{supportCase.reason}</strong></div>
            <div className="detail-row"><span>PSID</span><strong>{supportCase.facebookPSID}</strong></div>
            <div className="detail-row"><span>Assigned</span><strong>{supportCase.assignedToEmail ?? "N/A"}</strong></div>
            <p>{supportCase.summary}</p>
            <textarea className="text-area" onChange={(event) => setNotes(event.target.value)} placeholder="Resolution notes" value={notes} />
            <div className="button-row">
              <button className="secondary-button" onClick={() => claimMutation.mutate()} type="button">Claim</button>
              <button className="primary-button" onClick={() => resolveMutation.mutate()} type="button">Resolve</button>
              <button className="ghost-button" onClick={() => cancelMutation.mutate()} type="button">Cancel</button>
            </div>
          </div>

          <div className="card stack">
            <h2>Transcript excerpt</h2>
            <pre className="transcript">{supportCase.transcriptExcerpt}</pre>
          </div>

          <div className="card stack">
            <h2>Audit log</h2>
            {supportCase.auditLogs.map((log) => (
              <article className="list-item" key={log.id}>
                <div>
                  <strong>{log.action}</strong>
                  <div>{log.actorEmail}</div>
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
