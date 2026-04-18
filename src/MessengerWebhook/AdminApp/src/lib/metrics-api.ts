import type { MetricsSummary, VariantComparison, PipelineLatency, ConversationTrend } from "../types/metrics";

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const payload = (await response.json().catch(() => null)) as { error?: string } | null;
    throw new Error(payload?.error ?? `Request failed with ${response.status}`);
  }
  return (await response.json()) as T;
}

const API_BASE = "/admin/api";

export const metricsApi = {
  async fetchSummary(startDate: Date, endDate: Date): Promise<MetricsSummary> {
    const params = new URLSearchParams({
      startDate: startDate.toISOString(),
      endDate: endDate.toISOString()
    });

    const response = await fetch(`${API_BASE}/metrics/summary?${params}`, { credentials: "include" });
    return readJson<MetricsSummary>(response);
  },

  async fetchVariants(startDate: Date, endDate: Date): Promise<VariantComparison> {
    const params = new URLSearchParams({
      startDate: startDate.toISOString(),
      endDate: endDate.toISOString()
    });

    const response = await fetch(`${API_BASE}/metrics/variants?${params}`, { credentials: "include" });
    return readJson<VariantComparison>(response);
  },

  async fetchPipeline(startDate: Date, endDate: Date): Promise<PipelineLatency> {
    const params = new URLSearchParams({
      startDate: startDate.toISOString(),
      endDate: endDate.toISOString()
    });

    const response = await fetch(`${API_BASE}/metrics/pipeline?${params}`, { credentials: "include" });
    return readJson<PipelineLatency>(response);
  },

  async fetchTrends(startDate: Date, endDate: Date): Promise<ConversationTrend[]> {
    const params = new URLSearchParams({
      startDate: startDate.toISOString(),
      endDate: endDate.toISOString()
    });

    const response = await fetch(`${API_BASE}/metrics/trends?${params}`, { credentials: "include" });
    return readJson<ConversationTrend[]>(response);
  }
};
