export interface MetricsSummary {
  totalConversations: number;
  completionRate: number;
  escalationRate: number;
  abandonmentRate: number;
  avgMessagesPerConversation: number;
  avgPipelineLatencyMs: number;
}

export interface VariantComparison {
  control: MetricsSummary;
  treatment: MetricsSummary;
  statisticalSignificance: boolean;
  pValue: number;
}

export interface LatencyPercentiles {
  p50: number;
  p95: number;
  p99: number;
}

export interface PipelineLatency {
  emotion: LatencyPercentiles;
  tone: LatencyPercentiles;
  context: LatencyPercentiles;
  smallTalk: LatencyPercentiles;
  validation: LatencyPercentiles;
  total: LatencyPercentiles;
}

export interface ConversationTrend {
  date: string;
  completionRate: number;
  escalationRate: number;
  abandonmentRate: number;
  avgMessages: number;
}

export interface DateRange {
  startDate: Date;
  endDate: Date;
}

export type DateRangePreset = '7d' | '14d' | '30d' | 'custom';
