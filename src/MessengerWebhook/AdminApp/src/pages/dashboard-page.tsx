import { useQuery } from "@tanstack/react-query";
import { api } from "../lib/api";

export function DashboardPage() {
  const overviewQuery = useQuery({
    queryKey: ["dashboard"],
    queryFn: api.getDashboard
  });

  const overview = overviewQuery.data;

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <span className="eyebrow">Tổng quan</span>
          <h1>Bảng điều khiển vận hành</h1>
        </div>
      </div>

      {overviewQuery.isLoading ? <div className="card">Đang tải dashboard...</div> : null}
      {overviewQuery.isError ? <div className="error-box">{(overviewQuery.error as Error).message}</div> : null}

      {overview ? (
        <div className="stats-grid">
          <article className="stat-card">
            <span>Đơn chờ duyệt</span>
            <strong>{overview.pendingDrafts}</strong>
          </article>
          <article className="stat-card">
            <span>Submit lỗi</span>
            <strong>{overview.submitFailedDrafts}</strong>
          </article>
          <article className="stat-card">
            <span>Case đang mở</span>
            <strong>{overview.openSupportCases}</strong>
          </article>
          <article className="stat-card">
            <span>Case đã claim</span>
            <strong>{overview.claimedSupportCases}</strong>
          </article>
        </div>
      ) : null}
    </section>
  );
}
