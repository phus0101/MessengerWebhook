---
phase: 7.5
title: "Custom Dashboard"
effort: 6h
status: pending
dependencies: [7.3]
---

# Phase 7.5: Custom Dashboard

## Context

Build React admin dashboard to visualize A/B test results and pipeline metrics. Provides real-time insights into naturalness pipeline impact vs baseline.

**Related Files**:
- `src/MessengerWebhook/AdminApp/` - Existing React admin UI structure
- `src/MessengerWebhook/Controllers/MetricsController.cs` - Metrics API (Phase 7.3)
- `docs/system-architecture.md` - Admin UI architecture

## Overview

**Priority**: P1 (user decision: build custom dashboard)  
**Status**: Pending  
**Effort**: 6 hours  
**Dependencies**: Phase 7.3 (Metrics API)

## Key Insights

- Leverage existing AdminApp structure (React + TypeScript + Vite + shadcn/ui)
- Real-time updates via polling (30s interval) - simpler than WebSockets
- 3 focused views: A/B comparison, pipeline performance, conversation outcomes
- Export to CSV for external analysis (Excel, BI tools)
- Responsive design (desktop + tablet) - mobile not required for admin tool

## Requirements

### Functional

**Dashboard Views**:
1. **A/B Test Summary** - Control vs treatment comparison
   - Conversion rate by variant
   - Average conversation length
   - Escalation rate
   - CSAT score (Phase 7.6)
   - Statistical significance indicator

2. **Pipeline Performance** - Latency breakdown
   - Emotion detection latency
   - Tone analysis latency
   - Context enrichment latency
   - SmallTalk detection latency
   - Validation latency
   - Total pipeline overhead
   - P50, P95, P99 percentiles

3. **Conversation Outcomes** - Business metrics
   - Completion rate (conversation finished)
   - Abandonment rate (user stopped responding)
   - Escalation rate (transferred to human)
   - Average messages per conversation
   - Trend over time (line chart)

**Features**:
- Date range picker (last 7 days, 14 days, 30 days, custom)
- Auto-refresh toggle (on/off)
- Export to CSV (all views)
- Tenant filter (multi-tenant support)
- Loading states and error handling

### Non-Functional

- Dashboard loads in <2s (initial render)
- Chart rendering <500ms
- Responsive on desktop (1920x1080) and tablet (768x1024)
- Accessible (WCAG 2.1 AA - keyboard navigation, screen reader support)
- Zero impact on backend performance (read-only queries)

## Architecture

### Component Structure

```
AdminApp/src/
├── pages/Metrics/
│   ├── ABTestDashboard.tsx          # Main dashboard container
│   ├── ABTestSummary.tsx            # View 1: A/B comparison
│   ├── PipelinePerformance.tsx      # View 2: Latency breakdown
│   └── ConversationOutcomes.tsx     # View 3: Business metrics
├── components/Metrics/
│   ├── MetricsCard.tsx              # Reusable metric display card
│   ├── DateRangePicker.tsx          # Date range selector
│   ├── ExportButton.tsx             # CSV export functionality
│   └── StatisticalSignificance.tsx  # Significance badge
├── hooks/
│   ├── useMetrics.ts                # React Query hook for metrics API
│   └── useAutoRefresh.ts            # Polling logic
└── lib/
    ├── metrics-api.ts               # API client
    └── csv-export.ts                # CSV generation utility
```

### Data Flow

```
User Opens Dashboard → useMetrics() hook
    ↓
Fetch /api/metrics/summary?startDate=X&endDate=Y
Fetch /api/metrics/variants?startDate=X&endDate=Y
Fetch /api/metrics/pipeline?startDate=X&endDate=Y
    ↓
React Query caches responses (5min TTL)
    ↓
Auto-refresh polls every 30s (if enabled)
    ↓
Charts re-render with new data
```

### API Integration

**Endpoints (from Phase 7.3)**:
- `GET /api/metrics/summary` - Overall metrics
- `GET /api/metrics/variants` - A/B test comparison
- `GET /api/metrics/pipeline` - Pipeline latency breakdown

**Query Parameters**:
- `startDate` (ISO 8601)
- `endDate` (ISO 8601)
- `tenantId` (optional, for multi-tenant filtering)

**Response Format**:
```typescript
interface MetricsSummary {
  totalConversations: number;
  completionRate: number;
  escalationRate: number;
  abandonmentRate: number;
  avgMessagesPerConversation: number;
  avgPipelineLatencyMs: number;
}

interface VariantComparison {
  control: MetricsSummary;
  treatment: MetricsSummary;
  statisticalSignificance: boolean;
  pValue: number;
}

interface PipelineLatency {
  emotion: { p50: number; p95: number; p99: number };
  tone: { p50: number; p95: number; p99: number };
  context: { p50: number; p95: number; p99: number };
  smallTalk: { p50: number; p95: number; p99: number };
  validation: { p50: number; p95: number; p99: number };
  total: { p50: number; p95: number; p99: number };
}
```

## Related Code Files

### Files to Create

**1. Pages (3 files)**
- `src/MessengerWebhook/AdminApp/src/pages/Metrics/ABTestDashboard.tsx` - Main container with tabs
- `src/MessengerWebhook/AdminApp/src/pages/Metrics/ABTestSummary.tsx` - A/B comparison view
- `src/MessengerWebhook/AdminApp/src/pages/Metrics/PipelinePerformance.tsx` - Latency charts
- `src/MessengerWebhook/AdminApp/src/pages/Metrics/ConversationOutcomes.tsx` - Business metrics

**2. Components (4 files)**
- `src/MessengerWebhook/AdminApp/src/components/Metrics/MetricsCard.tsx` - Reusable card
- `src/MessengerWebhook/AdminApp/src/components/Metrics/DateRangePicker.tsx` - Date selector
- `src/MessengerWebhook/AdminApp/src/components/Metrics/ExportButton.tsx` - CSV export
- `src/MessengerWebhook/AdminApp/src/components/Metrics/StatisticalSignificance.tsx` - Badge

**3. Hooks (2 files)**
- `src/MessengerWebhook/AdminApp/src/hooks/useMetrics.ts` - React Query integration
- `src/MessengerWebhook/AdminApp/src/hooks/useAutoRefresh.ts` - Polling logic

**4. Utilities (2 files)**
- `src/MessengerWebhook/AdminApp/src/lib/metrics-api.ts` - API client
- `src/MessengerWebhook/AdminApp/src/lib/csv-export.ts` - CSV generation

**5. Types (1 file)**
- `src/MessengerWebhook/AdminApp/src/types/metrics.ts` - TypeScript interfaces

### Files to Modify

**1. `src/MessengerWebhook/AdminApp/src/App.tsx`** - Add metrics route
**2. `src/MessengerWebhook/AdminApp/src/components/Navigation.tsx`** - Add metrics nav link
**3. `src/MessengerWebhook/AdminApp/package.json`** - Add dependencies (recharts, react-query, date-fns)

## Implementation Steps

### Step 1: Setup Dependencies (30min)

```bash
cd src/MessengerWebhook/AdminApp
npm install @tanstack/react-query recharts date-fns
npm install -D @types/recharts
```

### Step 2: Create API Client (45min)

**File**: `src/lib/metrics-api.ts`

```typescript
export async function fetchMetricsSummary(
  startDate: Date,
  endDate: Date,
  tenantId?: string
): Promise<MetricsSummary> {
  const params = new URLSearchParams({
    startDate: startDate.toISOString(),
    endDate: endDate.toISOString(),
    ...(tenantId && { tenantId })
  });
  
  const response = await fetch(`/api/metrics/summary?${params}`);
  if (!response.ok) throw new Error('Failed to fetch metrics');
  return response.json();
}
```

### Step 3: Create React Query Hook (45min)

**File**: `src/hooks/useMetrics.ts`

```typescript
export function useMetrics(startDate: Date, endDate: Date, tenantId?: string) {
  return useQuery({
    queryKey: ['metrics', startDate, endDate, tenantId],
    queryFn: () => fetchMetricsSummary(startDate, endDate, tenantId),
    staleTime: 5 * 60 * 1000, // 5min cache
    refetchInterval: 30 * 1000 // 30s polling
  });
}
```

### Step 4: Build Reusable Components (1.5h)

**MetricsCard.tsx** - Display single metric with trend
**DateRangePicker.tsx** - shadcn/ui date picker with presets
**ExportButton.tsx** - Generate CSV from data
**StatisticalSignificance.tsx** - Badge showing p-value

### Step 5: Build Dashboard Views (2h)

**ABTestSummary.tsx** - Side-by-side comparison cards + bar chart
**PipelinePerformance.tsx** - Stacked bar chart for latency breakdown
**ConversationOutcomes.tsx** - Line chart for trends over time

### Step 6: Integrate into Admin UI (30min)

- Add route in `App.tsx`
- Add nav link in `Navigation.tsx`
- Test navigation and data loading

### Step 7: CSV Export (45min)

**File**: `src/lib/csv-export.ts`

```typescript
export function exportToCSV(data: any[], filename: string) {
  const csv = convertToCSV(data);
  const blob = new Blob([csv], { type: 'text/csv' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
}
```

### Step 8: Testing & Polish (1h)

- Test all 3 views with real data
- Verify date range filtering
- Test CSV export
- Check responsive layout on tablet
- Verify auto-refresh toggle

## Todo List

- [ ] Install dependencies (recharts, react-query, date-fns)
- [ ] Create TypeScript interfaces in `types/metrics.ts`
- [ ] Implement API client in `lib/metrics-api.ts`
- [ ] Create `useMetrics` hook with React Query
- [ ] Create `useAutoRefresh` hook for polling
- [ ] Build `MetricsCard` component
- [ ] Build `DateRangePicker` component
- [ ] Build `ExportButton` component
- [ ] Build `StatisticalSignificance` component
- [ ] Create `ABTestSummary` page
- [ ] Create `PipelinePerformance` page
- [ ] Create `ConversationOutcomes` page
- [ ] Create `ABTestDashboard` container
- [ ] Add metrics route to `App.tsx`
- [ ] Add metrics nav link to `Navigation.tsx`
- [ ] Implement CSV export utility
- [ ] Test dashboard with real data
- [ ] Verify responsive layout
- [ ] Test auto-refresh functionality
- [ ] Verify tenant filtering works

## Success Criteria

- [ ] Dashboard loads in <2s with 10K metrics
- [ ] All 3 views render correctly with real data
- [ ] Date range picker filters data correctly
- [ ] CSV export generates valid files
- [ ] Auto-refresh polls every 30s when enabled
- [ ] Charts render in <500ms
- [ ] Responsive on desktop (1920x1080) and tablet (768x1024)
- [ ] No console errors or warnings
- [ ] Statistical significance badge shows correct p-value
- [ ] Tenant filter isolates data correctly

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Chart library performance issues | Low | Medium | Use Recharts (optimized for React), lazy load charts |
| API latency causes slow dashboard | Medium | High | React Query caching (5min TTL), loading states |
| CSV export crashes on large datasets | Low | Medium | Limit export to 10K rows, add pagination |
| Auto-refresh causes memory leaks | Low | High | Cleanup intervals in useEffect, React Query handles this |
| Date range picker UX confusing | Medium | Low | Add presets (7d, 14d, 30d), clear labels |

## Security Considerations

- **Authorization**: Dashboard requires admin role (enforced by backend API)
- **Tenant Isolation**: Metrics API filters by tenantId (enforced in Phase 7.3)
- **XSS Prevention**: React escapes all user input by default
- **CSRF Protection**: API uses bearer token authentication
- **Data Exposure**: No sensitive user data displayed (only aggregated metrics)

## Backwards Compatibility

- New dashboard is additive (no breaking changes)
- Existing admin UI routes unchanged
- Metrics API (Phase 7.3) already supports date range queries
- No database migrations required

## Rollback Plan

**Rollback Steps**:
1. Remove metrics route from `App.tsx`
2. Remove metrics nav link from `Navigation.tsx`
3. Delete `pages/Metrics/` directory
4. Uninstall dependencies (optional)

**Data Impact**: None (dashboard is read-only)

## Next Steps

1. Complete Phase 7.3 (Metrics API) first
2. Verify API endpoints return correct data
3. Implement dashboard components
4. Test with real A/B test data (2-week test period)
5. Gather user feedback from admin users
6. Iterate on UX based on feedback

## Unresolved Questions

1. **Chart library choice**: Recharts vs Chart.js? (Recommendation: Recharts - better React integration)
2. **Auto-refresh default**: On or off by default? (Recommendation: Off - user opt-in)
3. **Export format**: CSV only or add JSON/Excel? (Recommendation: CSV only - simplest)
4. **Mobile support**: Add mobile layout or desktop/tablet only? (Recommendation: Desktop/tablet only - admin tool)
5. **Historical data**: Show trends beyond 90-day retention? (Recommendation: No - respect retention policy)
