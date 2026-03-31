import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { StatusPill } from "../components/status-pill";
import { api } from "../lib/api";
import { formatDate, formatMoney } from "../lib/format";

export function DraftOrdersPage() {
  const draftOrdersQuery = useQuery({
    queryKey: ["draft-orders"],
    queryFn: api.getDraftOrders
  });

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <span className="eyebrow">Draft orders</span>
          <h1>Đơn nháp</h1>
        </div>
      </div>

      {draftOrdersQuery.isLoading ? <div className="card">Đang tải đơn nháp...</div> : null}
      {draftOrdersQuery.isError ? <div className="error-box">{(draftOrdersQuery.error as Error).message}</div> : null}

      {draftOrdersQuery.data ? (
        <div className="table-card">
          <table>
            <thead>
              <tr>
                <th>Mã đơn</th>
                <th>Khách</th>
                <th>Trạng thái</th>
                <th>Risk</th>
                <th>Tổng tiền</th>
                <th>Tạo lúc</th>
              </tr>
            </thead>
            <tbody>
              {draftOrdersQuery.data.map((draft) => (
                <tr key={draft.id}>
                  <td>
                    <Link to={`/draft-orders/${draft.id}`}>{draft.draftCode}</Link>
                  </td>
                  <td>
                    <strong>{draft.customerName ?? "Khách Messenger"}</strong>
                    <div>{draft.customerPhone}</div>
                  </td>
                  <td><StatusPill value={draft.status} /></td>
                  <td><StatusPill value={draft.riskLevel} /></td>
                  <td>{formatMoney(draft.grandTotal)}</td>
                  <td>{formatDate(draft.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </section>
  );
}
