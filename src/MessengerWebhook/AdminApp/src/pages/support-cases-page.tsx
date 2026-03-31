import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { StatusPill } from "../components/status-pill";
import { api } from "../lib/api";
import { formatDate } from "../lib/format";

export function SupportCasesPage() {
  const supportCasesQuery = useQuery({
    queryKey: ["support-cases"],
    queryFn: api.getSupportCases
  });

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <span className="eyebrow">Human handoff</span>
          <h1>Support cases</h1>
        </div>
      </div>

      {supportCasesQuery.isLoading ? <div className="card">Đang tải support cases...</div> : null}
      {supportCasesQuery.isError ? <div className="error-box">{(supportCasesQuery.error as Error).message}</div> : null}

      {supportCasesQuery.data ? (
        <div className="table-card">
          <table>
            <thead>
              <tr>
                <th>Case</th>
                <th>PSID</th>
                <th>Lý do</th>
                <th>Trạng thái</th>
                <th>Tạo lúc</th>
              </tr>
            </thead>
            <tbody>
              {supportCasesQuery.data.map((supportCase) => (
                <tr key={supportCase.id}>
                  <td>
                    <Link to={`/support-cases/${supportCase.id}`}>{supportCase.id.slice(0, 8)}</Link>
                    <div>{supportCase.summary}</div>
                  </td>
                  <td>{supportCase.facebookPSID}</td>
                  <td>{supportCase.reason}</td>
                  <td><StatusPill value={supportCase.status} /></td>
                  <td>{formatDate(supportCase.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </section>
  );
}
