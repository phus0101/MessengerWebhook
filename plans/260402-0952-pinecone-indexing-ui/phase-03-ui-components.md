# Phase 3: React UI Components

## Priority
P1 (user-facing feature)

## Status
pending

## Overview

Build React UI components for triggering indexing and displaying real-time progress. Components follow existing admin UI patterns (React Query, Tailwind CSS, existing component structure).

## Key Insights

- Existing admin UI uses React Query for data fetching
- Pattern: `api.ts` for API calls, `types.ts` for TypeScript types
- Dashboard uses card-based layout with stats
- Existing pages: dashboard, draft-orders, support-cases, product-mappings
- Need new page: vector-search-page.tsx

## Requirements

### Functional
- Button to start indexing (disabled when running)
- Real-time progress display (polls every 2s)
- Show: total, indexed, remaining, current product, progress bar
- Error handling with user-friendly messages
- Stop polling when job completes

### Non-functional
- Responsive design (mobile-friendly)
- Accessible (ARIA labels, keyboard navigation)
- Consistent with existing admin UI style
- Loading states for better UX

## Architecture

### Component Structure

```
VectorSearchPage
├── IndexingControlCard (button + status)
├── ProgressDisplay (stats + progress bar)
└── ErrorDisplay (error messages)
```

### State Management

```typescript
// React Query for API calls
const startMutation = useMutation(api.startIndexing);
const statusQuery = useQuery({
  queryKey: ['indexing-status', jobId],
  queryFn: () => api.getIndexingStatus(jobId),
  refetchInterval: (data) =>
    data?.status === 'Running' ? 2000 : false,
  enabled: !!jobId
});
```

## Related Code Files

**New:**
- `src/MessengerWebhook/AdminApp/src/pages/vector-search-page.tsx`

**Modify:**
- `src/MessengerWebhook/AdminApp/src/lib/api.ts` (add API methods)
- `src/MessengerWebhook/AdminApp/src/lib/types.ts` (add TypeScript types)
- `src/MessengerWebhook/AdminApp/src/components/layout.tsx` (add nav link)
- `src/MessengerWebhook/AdminApp/src/main.tsx` (add route)

## Implementation Steps

1. **Add TypeScript types** (`types.ts`):
   ```typescript
   export interface IndexingStatus {
     jobId: string;
     status: 'NotStarted' | 'Running' | 'Completed' | 'Failed';
     totalProducts: number;
     indexedProducts: number;
     progressPercentage: number;
     currentProductId?: string;
     currentProductName?: string;
     startedAt: string;
     completedAt?: string;
     errorMessage?: string;
   }

   export interface StartIndexingResponse {
     jobId: string;
     message: string;
   }
   ```

2. **Add API methods** (`api.ts`):
   ```typescript
   startIndexing(csrfToken: string) {
     return postJson<StartIndexingResponse>(
       '/admin/api/vector-search/index-all',
       csrfToken
     );
   },
   getIndexingStatus(jobId: string) {
     return fetch(
       `/admin/api/vector-search/index-status/${jobId}`,
       { credentials: 'include' }
     ).then(response => readJson<IndexingStatus>(response));
   }
   ```

3. **Create VectorSearchPage component**:
   - Use existing page structure (page-section, page-header)
   - Add "Start Indexing" button
   - Show progress card when job active
   - Poll status every 2s during indexing
   - Stop polling on completion/failure

4. **Add navigation link** (`layout.tsx`):
   ```tsx
   <NavLink to="/vector-search">Vector Search</NavLink>
   ```

5. **Add route** (`main.tsx`):
   ```tsx
   <Route path="/vector-search" element={<VectorSearchPage />} />
   ```

## Todo List

- [ ] Add TypeScript types to `types.ts`
- [ ] Add API methods to `api.ts`
- [ ] Create `vector-search-page.tsx` component
- [ ] Implement start indexing button with mutation
- [ ] Implement progress polling with React Query
- [ ] Add progress bar component (0-100%)
- [ ] Display stats: total, indexed, remaining
- [ ] Display current product being indexed
- [ ] Add error handling UI
- [ ] Add loading states
- [ ] Style with Tailwind (match existing pages)
- [ ] Add navigation link in layout
- [ ] Add route in main.tsx
- [ ] Test in browser (start, progress, completion)

## Success Criteria

- Button triggers indexing and shows immediate feedback
- Progress updates every 2-3 seconds
- Progress bar animates smoothly
- Current product name displays during indexing
- Completion message shows when done
- Error messages display clearly
- UI responsive on mobile devices
- Polling stops after job completes

## UI Mockup (Text)

```
┌─────────────────────────────────────────┐
│ Vector Search Management                │
├─────────────────────────────────────────┤
│                                         │
│ ┌─────────────────────────────────────┐ │
│ │ Pinecone Indexing                   │ │
│ │                                     │ │
│ │ [Start Indexing Products]           │ │
│ │                                     │ │
│ │ Status: Running                     │ │
│ │ ████████████░░░░░░░░░░ 60%         │ │
│ │                                     │ │
│ │ Progress: 90 / 150 products         │ │
│ │ Remaining: 60 products              │ │
│ │                                     │ │
│ │ Currently indexing:                 │ │
│ │ Serum Vitamin C (PROD-123)         │ │
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Polling continues after page close | useEffect cleanup stops query |
| Network error during polling | React Query retry + error UI |
| Job expires during polling | Handle 404, show "Job expired" message |
| Multiple tabs start indexing | Backend prevents concurrent jobs (409) |

## Accessibility Considerations

- Button has aria-label="Start product indexing"
- Progress bar has role="progressbar" with aria-valuenow
- Status updates announced via aria-live="polite"
- Keyboard navigation works for all controls

## Next Steps

After completion:
- Proceed to Phase 4 (integration testing)
- Manual testing in browser
- Screenshot for documentation
