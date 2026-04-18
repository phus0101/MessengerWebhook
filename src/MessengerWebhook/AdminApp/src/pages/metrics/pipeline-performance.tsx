import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { usePipelineLatency } from '../../hooks/use-metrics';
import { ExportButton } from '../../components/metrics/export-button';
import type { DateRange } from '../../types/metrics';

interface PipelinePerformanceProps {
  dateRange: DateRange;
  tenantId?: string;
}

export function PipelinePerformance({ dateRange, tenantId }: PipelinePerformanceProps) {
  const { data, isLoading, error } = usePipelineLatency(dateRange, tenantId);

  if (isLoading) {
    return <div className="loading-state">Đang tải dữ liệu pipeline...</div>;
  }

  if (error) {
    return <div className="error-state">Lỗi: {(error as Error).message}</div>;
  }

  if (!data) {
    return <div className="empty-state">Không có dữ liệu</div>;
  }

  const chartData = [
    {
      stage: 'Emotion',
      P50: data.emotion.p50,
      P95: data.emotion.p95,
      P99: data.emotion.p99
    },
    {
      stage: 'Tone',
      P50: data.tone.p50,
      P95: data.tone.p95,
      P99: data.tone.p99
    },
    {
      stage: 'Context',
      P50: data.context.p50,
      P95: data.context.p95,
      P99: data.context.p99
    },
    {
      stage: 'SmallTalk',
      P50: data.smallTalk.p50,
      P95: data.smallTalk.p95,
      P99: data.smallTalk.p99
    },
    {
      stage: 'Validation',
      P50: data.validation.p50,
      P95: data.validation.p95,
      P99: data.validation.p99
    },
    {
      stage: 'Total',
      P50: data.total.p50,
      P95: data.total.p95,
      P99: data.total.p99
    }
  ];

  const exportData = chartData.map(item => ({
    stage: item.stage,
    p50_ms: item.P50,
    p95_ms: item.P95,
    p99_ms: item.P99
  }));

  return (
    <div className="pipeline-performance">
      <div className="summary-header">
        <h2>Hiệu suất Pipeline - Phân tích độ trễ</h2>
        <ExportButton data={exportData} filename="pipeline-latency" />
      </div>

      <div className="performance-summary">
        <div className="summary-card">
          <h3>Tổng độ trễ Pipeline</h3>
          <div className="latency-stats">
            <div className="stat-item">
              <span className="stat-label">P50 (Median):</span>
              <span className="stat-value">{data.total.p50.toFixed(1)} ms</span>
            </div>
            <div className="stat-item">
              <span className="stat-label">P95:</span>
              <span className="stat-value">{data.total.p95.toFixed(1)} ms</span>
            </div>
            <div className="stat-item">
              <span className="stat-label">P99:</span>
              <span className="stat-value">{data.total.p99.toFixed(1)} ms</span>
            </div>
          </div>
        </div>
      </div>

      <div className="chart-section">
        <h3>Độ trễ theo giai đoạn (Percentiles)</h3>
        <ResponsiveContainer width="100%" height={400}>
          <BarChart data={chartData}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="stage" />
            <YAxis label={{ value: 'Độ trễ (ms)', angle: -90, position: 'insideLeft' }} />
            <Tooltip />
            <Legend />
            <Bar dataKey="P50" fill="#8884d8" name="P50 (Median)" />
            <Bar dataKey="P95" fill="#ffc658" name="P95" />
            <Bar dataKey="P99" fill="#ff7c7c" name="P99" />
          </BarChart>
        </ResponsiveContainer>
      </div>

      <div className="stage-breakdown">
        <h3>Chi tiết từng giai đoạn</h3>
        <table className="latency-table">
          <thead>
            <tr>
              <th>Giai đoạn</th>
              <th>P50 (ms)</th>
              <th>P95 (ms)</th>
              <th>P99 (ms)</th>
            </tr>
          </thead>
          <tbody>
            {chartData.map((item) => (
              <tr key={item.stage}>
                <td>{item.stage}</td>
                <td>{item.P50.toFixed(1)}</td>
                <td>{item.P95.toFixed(1)}</td>
                <td>{item.P99.toFixed(1)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
