# CLAUDE.md Quality Report

**Generated:** 2026-04-02 13:37:05
**Project:** MessengerWebhook (Facebook Messenger Sales Bot)

## Summary
- Files found: 1
- Average score: 68/100
- Files needing update: 1

---

## File-by-File Assessment

### 1. ./CLAUDE.md (Project Root)
**Score: 68/100 (Grade: C)**

| Criterion | Score | Notes |
|-----------|-------|-------|
| Commands/workflows | 8/20 | Missing build, run, test, migration commands |
| Architecture clarity | 14/20 | References docs but no quick overview |
| Non-obvious patterns | 12/15 | Good coverage of hooks, modularization |
| Conciseness | 13/15 | Well-structured, not verbose |
| Currency | 11/15 | Missing recent tech stack (Pinecone, pgvector) |
| Actionability | 10/15 | Workflow references good, but missing concrete commands |

**Issues:**

1. **Missing Quick Start Commands**
   - No build command (`dotnet build`)
   - No run command (`dotnet run` or `dotnet watch`)
   - No test command (`dotnet test`)
   - No database migration command (`dotnet ef database update`)

2. **Missing Environment Setup**
   - No mention of required .env file
   - No mention of Docker for PostgreSQL
   - No mention of required API keys (Facebook, Gemini, Pinecone, Vertex AI)
   - No mention of .NET SDK version requirement (9.0.200)

3. **Missing Tech Stack Overview**
   - ASP.NET Core 8.0 minimal APIs
   - PostgreSQL with pgvector extension
   - Pinecone v2.0.0 for vector search
   - Google Gemini AI for embeddings
   - React + TypeScript admin UI
   - Multi-tenant architecture

4. **Missing Database Gotchas**
   - Docker PostgreSQL runs on port 5433 (not default 5432)
   - Requires pgvector extension
   - Tenant isolation via TenantId in all queries

5. **Missing Testing Patterns**
   - Unit tests in `tests/MessengerWebhook.UnitTests/`
   - Integration tests in `tests/MessengerWebhook.IntegrationTests/`
   - No mention of test database setup

**Recommended Additions:**

### Quick Start Section
```bash
# Prerequisites
- .NET SDK 9.0.200+
- Docker (for PostgreSQL)
- Node.js 18+ (for admin UI)

# Setup
1. Copy .env.example to .env and fill in API keys
2. Start PostgreSQL: docker-compose up -d
3. Run migrations: dotnet ef database update --project src/MessengerWebhook
4. Run app: dotnet run --project src/MessengerWebhook
5. Admin UI: cd src/MessengerWebhook/AdminApp && npm install && npm run dev

# Development
dotnet watch --project src/MessengerWebhook  # Hot reload
dotnet test                                   # Run all tests
```

### Environment Variables Section
```bash
# Required API Keys (.env file)
FACEBOOK_APP_SECRET=xxx
FACEBOOK_PAGE_ACCESS_TOKEN=xxx
WEBHOOK_VERIFY_TOKEN=xxx
GEMINI_API_KEY=xxx
PINECONE_API_KEY=xxx
VERTEX_AI_PROJECT_ID=xxx
```

### Tech Stack Section
```
- ASP.NET Core 8.0 (minimal APIs)
- PostgreSQL 15+ with pgvector extension
- Pinecone v2.0.0 (vector search)
- Google Gemini AI (embeddings + chat)
- React + TypeScript (admin UI)
- Multi-tenant architecture (TenantId isolation)
```

### Database Gotchas Section
```
- PostgreSQL runs on port 5433 (Docker), not 5432
- Requires pgvector extension for vector embeddings
- All queries MUST filter by TenantId for isolation
- Use MessengerBotDbContextFactory for design-time migrations
```

### Testing Section
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/MessengerWebhook.UnitTests
dotnet test tests/MessengerWebhook.IntegrationTests

# Integration tests require PostgreSQL running
docker-compose up -d
```

---

## Recommendations

### Priority 1 (Critical - Missing Commands)
Add Quick Start section with build/run/test commands. Developers need these immediately.

### Priority 2 (High - Environment Setup)
Add Environment Variables section. App won't run without proper .env configuration.

### Priority 3 (High - Database Gotchas)
Document port 5433 and pgvector requirement. These are non-obvious and cause confusion.

### Priority 4 (Medium - Tech Stack)
Add Tech Stack overview. Helps new developers understand architecture quickly.

### Priority 5 (Medium - Testing)
Document testing patterns and commands. Important for development workflow.

---

## User Tips

- **`#` key shortcut**: Press `#` during a Claude session to auto-incorporate learnings into CLAUDE.md
- **Keep it concise**: CLAUDE.md should be human-readable; dense is better than verbose
- **Actionable commands**: All documented commands should be copy-paste ready
- **Use `.claude.local.md`**: For personal preferences not shared with team (add to `.gitignore`)
- **Global defaults**: Put user-wide preferences in `~/.claude/CLAUDE.md`

---

## Next Steps

1. Review this quality report
2. Approve proposed additions
3. Apply updates to CLAUDE.md
4. Test commands to ensure they work
5. Commit updated CLAUDE.md
