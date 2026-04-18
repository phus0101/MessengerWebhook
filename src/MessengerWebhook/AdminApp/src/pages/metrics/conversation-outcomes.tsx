import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { format } from 'date-fns';
import { useConversationTrends, useMetricsSummary } from '../../hooks/use-metrics';
import { MetricsCard } from '../../components/metrics/metrics-card';
import { ExportButton } from '../../components/metrics/export-button';
import type { DateRange } from '../../types/metrics';

interface ConversationOutcomesProps {
  dateRange: DateRange;
  tenantId?: string;
}

export function ConversationOutcomes({ dateRange, tenantId }: ConversationOutcomesProps) {
  const { data: trends, isLoading: trendsLoading, error: trendsError } = useConversationTrends(dateRange, tenantId);
  const { data: summary, isLoading: summaryLoading, error: summaryError } = useMetricsSummary(dateRange, tenantId);

  const isLoading = trendsLoading || summaryLoading;
  const error = trendsError || summaryError;

  if (isLoading) {
    return <div className="loading-state">Đang tải dữ liệu kết quả hội thoại...</div>;
  }

  if (error) {
    return <div className="error-state">Lỗi: {(error as Error).message}</div>;
  }

  if (!trends || !summary) {
    return <div className="empty-state">Không có dữ liệu</div>;
  }

  const chartData = trends.map(item => ({
    date: format(new Date(item.date), 'dd/MM'),
    'Hoàn thành': (item.completionRate * 100).toFixed(1),
    'Chuyển giao': (item.escalationRate * 100).toFixed(1),
    'Bỏ cuộc': (item.abandonmentRate * 100).toFixed(1)
  }));

  const exportData = trends.map(item => ({
    date: item.date,
    completionRate: item.completionRate,
    escalationRate: item.escalationRate,
    abandonmentRate: item.abandonmentRate,
    avgMessages: item.avgMessages
  }));

  return (
    <div className="conversation-outcomes">
      <div className="summary-header">
        <h2>Kết quả hội thoại - Xu hướng theo thời gian</h2>
        <ExportButton data={exportData} filename="conversation-outcomes" />
      </div>

      <div className="metrics-grid">
        <MetricsCard
          title="Tổng hội thoại"
          value={summary.totalConversations.toLocaleString()}
          subtitle="Trong khoảng thời gian đã chọn"
        />
        <MetricsCard
          title="Tỷ lệ hoàn thành"
          value={`${(summary.completionRate * 100).toFixed(1)}%`}
          variant={summary.completionRate >= 0.7 ? 'success' : 'warning'}
        />
        <MetricsCard
          title="Tỷ lệ chuyển giao"
          value={`${(summary.escalationRate * 100).toFixed(1)}%`}
          variant={summary.escalationRate <= 0.2 ? 'success' : 'warning'}
        />
        <MetricsCard
          title="Tỷ lệ bỏ cuộc"
          value={`${(summary.abandonmentRate * 100).toFixed(1)}%`}
          variant={summary.abandonmentRate <= 0.15 ? 'success' : 'danger'}
        />
        <MetricsCard
          title="Tin nhắn TB/hội thoại"
          value={summary.avgMessagesPerConversation.toFixed(1)}
        />
        <MetricsCard
          title="Độ trễ pipeline TB"
          value={`${summary.avgPipelineLatencyMs.toFixed(0)} ms`}
          variant={summary.avgPipelineLatencyMs <= 500 ? 'success' : 'warning'}
        />
      </div>

      <div className="chart-section">
        <h3>Xu hướng theo thời gian</h3>
        <ResponsiveContainer width="100%" height={400}>
          <LineChart data={chartData}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="date" />
            <YAxis label={{ value: 'Phần trăm (%)', angle: -90, position: 'insideLeft' }} />
            <Tooltip />
            <Legend />
            <Line
              type="monotone"
              dataKey="Hoàn thành"
              stroke="#82ca9d"
              strokeWidth={2}
              dot={{ r: 4 }}
            />
            <Line
              type="monotone"
              dataKey="Chuyển giao"
              stroke="#ffc658"
              strokeWidth={2}
              dot={{ r: 4 }}
            />
            <Line
              type="monotone"
              dataKey="Bỏ cuộc"
              stroke="#ff7c7c"
              strokeWidth={2}
              dot={{ r: 4 }}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>

      <div className="insights-section">
        <h3>Phân tích</h3>
        <div className="insights-grid">
          <div className="insight-card">
            <h4>Hiệu suất tổng thể</h4>
            <p>
              {summary.completionRate >= 0.7
                ? '✓ Tỷ lệ hoàn thành tốt (≥70%)'
                : '⚠ Tỷ lệ hoàn thành cần cải thiện (<70%)'}
            </p>
          </div>
          <div className="insight-card">
            <h4>Chuyển giao con người</h4>
            <p>
              {summary.escalationRate <= 0.2
                ? '✓ Tỷ lệ chuyển giao thấp (≤20%)'
                : '⚠ Tỷ lệ chuyển giao cao (>20%)'}
            </p>
          </div>
          <div className="insight-card">
            <h4>Trải nghiệm người dùng</h4>
            <p>
              {summary.abandonmentRate <= 0.15
                ? '✓ Tỷ lệ bỏ cuộc thấp (≤15%)'
                : '⚠ Tỷ lệ bỏ cuộc cao (>15%)'}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
