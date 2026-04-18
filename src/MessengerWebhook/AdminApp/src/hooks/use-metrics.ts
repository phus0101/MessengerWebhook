import { useQuery } from "@tanstack/react-query";
import { metricsApi } from "../lib/metrics-api";
import type { DateRange } from "../types/metrics";

export function useMetricsSummary(dateRange: DateRange) {
  return useQuery({
    queryKey: ['metrics', 'summary', dateRange.startDate.toISOString(), dateRange.endDate.toISOString()],
    queryFn: () => metricsApi.fetchSummary(dateRange.startDate, dateRange.endDate),
    staleTime: 5 * 60 * 1000, // 5min cache
    refetchInterval: 30 * 1000 // 30s polling
  });
}

export function useVariantComparison(dateRange: DateRange) {
  return useQuery({
    queryKey: ['metrics', 'variants', dateRange.startDate.toISOString(), dateRange.endDate.toISOString()],
    queryFn: () => metricsApi.fetchVariants(dateRange.startDate, dateRange.endDate),
    staleTime: 5 * 60 * 1000,
    refetchInterval: 30 * 1000
  });
}

export function usePipelineLatency(dateRange: DateRange) {
  return useQuery({
    queryKey: ['metrics', 'pipeline', dateRange.startDate.toISOString(), dateRange.endDate.toISOString()],
    queryFn: () => metricsApi.fetchPipeline(dateRange.startDate, dateRange.endDate),
    staleTime: 5 * 60 * 1000,
    refetchInterval: 30 * 1000
  });
}

export function useConversationTrends(dateRange: DateRange) {
  return useQuery({
    queryKey: ['metrics', 'trends', dateRange.startDate.toISOString(), dateRange.endDate.toISOString()],
    queryFn: () => metricsApi.fetchTrends(dateRange.startDate, dateRange.endDate),
    staleTime: 5 * 60 * 1000,
    refetchInterval: 30 * 1000
  });
}
