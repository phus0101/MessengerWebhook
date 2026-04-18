import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { useVariantComparison } from '../../hooks/use-metrics';
import { MetricsCard } from '../../components/metrics/metrics-card';
import { StatisticalSignificance } from '../../components/metrics/statistical-significance';
import { ExportButton } from '../../components/metrics/export-button';
import type { DateRange } from '../../types/metrics';

interface ABTestSummaryProps {
  dateRange: DateRange;
  tenantId?: string;
}

export function ABTestSummary({ dateRange, tenantId }: ABTestSummaryProps) {
  const { data, isLoading, error } = useVariantComparison(dateRange, tenantId);

  if (isLoading) {
    return <div className="loading-state">Đang tải dữ liệu A/B test...</div>;
  }

  if (error) {
    return <div className="error-state">Lỗi: {(error as Error).message}</div>;
  }

  if (!data) {
    return <div className="empty-state">Không có dữ liệu</div>;
  }

  const chartData = [
    {
      metric: 'Tỷ lệ hoàn thành',
      Control: (data.control.completionRate * 100).toFixed(1),
      Treatment: (data.treatment.completionRate * 100).toFixed(1)
    },
    {
      metric: 'Tỷ lệ chuyển giao',
      Control: (data.control.escalationRate * 100).toFixed(1),
      Treatment: (data.treatment.escalationRate * 100).toFixed(1)
    },
    {
      metric: 'Tỷ lệ bỏ cuộc',
      Control: (data.control.abandonmentRate * 100).toFixed(1),
      Treatment: (data.treatment.abandonmentRate * 100).toFixed(1)
    }
  ];

  const exportData = [
    {
      variant: 'Control',
      totalConversations: data.control.totalConversations,
      completionRate: data.control.completionRate,
      escalationRate: data.control.escalationRate,
      abandonmentRate: data.control.abandonmentRate,
      avgMessages: data.control.avgMessagesPerConversation,
      avgLatencyMs: data.control.avgPipelineLatencyMs
    },
    {
      variant: 'Treatment',
      totalConversations: data.treatment.totalConversations,
      completionRate: data.treatment.completionRate,
      escalationRate: data.treatment.escalationRate,
      abandonmentRate: data.treatment.abandonmentRate,
      avgMessages: data.treatment.avgMessagesPerConversation,
      avgLatencyMs: data.treatment.avgPipelineLatencyMs
    }
  ];

  return (
    <div className="ab-test-summary">
      <div className="summary-header">
        <h2>So sánh A/B Test: Control vs Treatment</h2>
        <div className="header-actions">
          <StatisticalSignificance
            pValue={data.pValue}
            isSignificant={data.statisticalSignificance}
          />
          <ExportButton data={exportData} filename="ab-test-comparison" />
        </div>
      </div>

      <div className="metrics-grid">
        <div className="variant-section">
          <h3>Control (Baseline)</h3>
          <div className="cards-row">
            <MetricsCard
              title="Tổng hội thoại"
              value={data.control.totalConversations.toLocaleString()}
            />
            <MetricsCard
              title="Tỷ lệ hoàn thành"
              value={`${(data.control.completionRate * 100).toFixed(1)}%`}
              variant="default"
            />
            <MetricsCard
              title="Tỷ lệ chuyển giao"
              value={`${(data.control.escalationRate * 100).toFixed(1)}%`}
              variant="warning"
            />
            <MetricsCard
              title="Tin nhắn TB"
              value={data.control.avgMessagesPerConversation.toFixed(1)}
            />
          </div>
        </div>

        <div className="variant-section">
          <h3>Treatment (Naturalness Pipeline)</h3>
          <div className="cards-row">
            <MetricsCard
              title="Tổng hội thoại"
              value={data.treatment.totalConversations.toLocaleString()}
            />
            <MetricsCard
              title="Tỷ lệ hoàn thành"
              value={`${(data.treatment.completionRate * 100).toFixed(1)}%`}
              variant={data.treatment.completionRate > data.control.completionRate ? 'success' : 'default'}
            />
            <MetricsCard
              title="Tỷ lệ chuyển giao"
              value={`${(data.treatment.escalationRate * 100).toFixed(1)}%`}
              variant={data.treatment.escalationRate < data.control.escalationRate ? 'success' : 'warning'}
            />
            <MetricsCard
              title="Tin nhắn TB"
              value={data.treatment.avgMessagesPerConversation.toFixed(1)}
            />
          </div>
        </div>
      </div>

      <div className="chart-section">
        <h3>So sánh trực quan</h3>
        <ResponsiveContainer width="100%" height={300}>
          <BarChart data={chartData}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="metric" />
            <YAxis label={{ value: 'Phần trăm (%)', angle: -90, position: 'insideLeft' }} />
            <Tooltip />
            <Legend />
            <Bar dataKey="Control" fill="#8884d8" />
            <Bar dataKey="Treatment" fill="#82ca9d" />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
