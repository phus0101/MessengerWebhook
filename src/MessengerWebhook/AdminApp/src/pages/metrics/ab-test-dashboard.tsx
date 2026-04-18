import { useState } from 'react';
import { subDays } from 'date-fns';
import { DateRangePicker } from '../../components/metrics/date-range-picker';
import { ABTestSummary } from './ab-test-summary';
import { PipelinePerformance } from './pipeline-performance';
import { ConversationOutcomes } from './conversation-outcomes';
import type { DateRange } from '../../types/metrics';

type TabView = 'ab-test' | 'pipeline' | 'outcomes';

export function ABTestDashboard() {
  const [activeTab, setActiveTab] = useState<TabView>('ab-test');
  const [dateRange, setDateRange] = useState<DateRange>({
    startDate: subDays(new Date(), 7),
    endDate: new Date()
  });

  return (
    <div className="ab-test-dashboard">
      <div className="dashboard-header">
        <h1>A/B Testing Metrics Dashboard</h1>
        <p className="dashboard-subtitle">
          Phân tích hiệu suất Naturalness Pipeline vs Baseline
        </p>
      </div>

      <div className="dashboard-controls">
        <DateRangePicker value={dateRange} onChange={setDateRange} />
      </div>

      <div className="dashboard-tabs">
        <button
          type="button"
          className={`tab-button ${activeTab === 'ab-test' ? 'active' : ''}`}
          onClick={() => setActiveTab('ab-test')}
        >
          So sánh A/B Test
        </button>
        <button
          type="button"
          className={`tab-button ${activeTab === 'pipeline' ? 'active' : ''}`}
          onClick={() => setActiveTab('pipeline')}
        >
          Hiệu suất Pipeline
        </button>
        <button
          type="button"
          className={`tab-button ${activeTab === 'outcomes' ? 'active' : ''}`}
          onClick={() => setActiveTab('outcomes')}
        >
          Kết quả hội thoại
        </button>
      </div>

      <div className="dashboard-content">
        {activeTab === 'ab-test' && <ABTestSummary dateRange={dateRange} />}
        {activeTab === 'pipeline' && <PipelinePerformance dateRange={dateRange} />}
        {activeTab === 'outcomes' && <ConversationOutcomes dateRange={dateRange} />}
      </div>
    </div>
  );
}
