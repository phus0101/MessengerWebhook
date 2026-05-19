import csv

# Đọc comprehensive tasks
with open('plans/jira-import-ag247-comprehensive-tasks.csv', 'r', encoding='utf-8') as f:
    reader = csv.DictReader(f)
    all_tasks = list(reader)

# Danh sách 78 issues đã có trên Jira
existing_summaries = {
    'Multi-Tenant Messenger Chatbot Platform',
    'Phase 1: Quick Reply Handler',
    'Create QuickReply model',
    'Create ProductMappingService',
    'Create GiftSelectionService',
    'Create FreeshipCalculator',
    'Phase 2A: Nobita CRM API Integration',
    'Research Nobita API documentation',
    'Create NobitaApiClient service',
    'Implement customer lookup by phone',
    'Implement order history retrieval',
    'Phase 2B: Draft Order + Email Notification System',
    'Create DraftOrder entity',
    'Create DraftOrderRepository',
    'Implement SMTP email service',
    'Create email templates',
    'Implement draft order workflow',
    'Phase 2C: Customer Tracking & Risk Scoring',
    'Create Customer entity',
    'Create CustomerRepository',
    'Create CustomerTrackingService',
    'Implement risk scoring algorithm',
    'Phase 3: Simplified State Machine (6 states)',
    'Design 6-state flow diagram',
    'Create new ConversationState enum',
    'Implement IdleStateHandler (new)',
    'Implement QuickReplyStateHandler',
    'Implement ConsultingStateHandler',
    'Phase 4: Human Handoff System',
    'Create HandoffSession entity',
    'Create HandoffSessionRepository',
    'Implement handoff trigger detection',
    'Create HandoffStateHandler',
    'Implement handoff email notification',
    'Phase 5: Multi-Tenant Architecture',
    'Phase 5A: Database Schema Migration',
    'Create Tenant entity',
    'Create Branch entity',
    'Create TenantEntity base class',
    'Update all entities with TenantId',
    'Create and apply EF migration',
    'Seed Múi Xù as first tenant',
    'Migrate existing data to first tenant',
    'Phase 5B: Request Routing & Context',
    'Create ITenantContext service',
    'Create TenantResolutionMiddleware',
    'Add EF Core global query filters',
    'Create BranchRepository',
    'Add routing tests',
    'Phase 5C: Caching Layer (Redis)',
    'Setup Redis infrastructure',
    'Create TenantAwareCache service',
    'Create HybridCache (L1+L2)',
    'Update repositories with caching',
    'Add cache tests',
    'Phase 5D: Security Hardening',
    'Enable PostgreSQL Row-Level Security',
    'Create RLS policies',
    'Implement field encryption',
    'Create audit log system',
    'Add security tests',
    'Phase 5E: Testing & Validation',
    'Write unit tests',
    'Write integration tests',
    'Write E2E tests',
    'Run performance benchmarks',
    'Security audit',
    'Phase 6: Livestream Auto-Reply',
    'Research Facebook Live API',
    'Create LivestreamSession entity',
    'Implement livestream webhook handler',
    'Implement auto-reply logic',
    'Implement comment hiding',
    'Phase 7: Testing & Production Deployment',
    'Run full test suite',
    'Performance optimization',
    'Security hardening review',
    'Setup production infrastructure',
    'Create deployment documentation',
    'Deploy to staging',
    'User acceptance testing'
}

# Tìm tasks chưa có trên Jira
missing_tasks = []
for task in all_tasks:
    summary = task['Summary'].strip()
    if summary not in existing_summaries:
        missing_tasks.append(task)

print(f'Tổng số tasks trong CSV: {len(all_tasks)}')
print(f'Số tasks đã có trên Jira: {len(existing_summaries)}')
print(f'Số tasks còn thiếu: {len(missing_tasks)}')

# Ghi ra file CSV mới
if missing_tasks:
    with open('plans/jira-import-ag247-missing-tasks.csv', 'w', encoding='utf-8', newline='') as f:
        fieldnames = missing_tasks[0].keys()
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(missing_tasks)
    print('\nĐã tạo file: plans/jira-import-ag247-missing-tasks.csv')
    print(f'\nDanh sách {len(missing_tasks)} tasks còn thiếu:')
    for i, task in enumerate(missing_tasks[:10], 1):
        print(f'{i}. {task["Summary"]}')
    if len(missing_tasks) > 10:
        print(f'... và {len(missing_tasks) - 10} tasks khác')
