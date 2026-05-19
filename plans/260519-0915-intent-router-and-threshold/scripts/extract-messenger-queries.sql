-- =========================================================================
-- Extract customer queries from prod conversation history for threshold tuning
-- Target: Phase 03 (Adaptive Threshold + Query Expansion)
-- Run:   psql -h localhost -p 5433 -U <user> -d <db> -f extract-messenger-queries.sql > queries.csv
-- =========================================================================
--
-- Output columns (CSV-friendly via \copy):
--   conversation_id, tenant_id, created_at_utc, role, content,
--   token_count, char_count, next_bot_reply, next_bot_was_fallback
--
-- Filters:
--   * Only role='user' messages (customer queries)
--   * Only last 90 days (tunable)
--   * Excludes messages < 2 chars (noise) and > 200 chars (likely paste)
--   * Joins next bot reply for each user message (LATERAL) for fallback detection
--
-- IMPORTANT: Output CSV contains customer PII. Store under
-- plans/260519-0915-intent-router-and-threshold/data/ (gitignored).
-- =========================================================================

\copy (
  WITH user_msgs AS (
    SELECT
      m."SessionId"        AS conversation_id,
      m."TenantId"         AS tenant_id,
      m."CreatedAt"        AS created_at_utc,
      m."Role"             AS role,
      m."Content"          AS content,
      array_length(regexp_split_to_array(trim(m."Content"), '\s+'), 1) AS token_count,
      length(m."Content")  AS char_count,
      m."Id"               AS msg_id
    FROM "ConversationMessages" m
    WHERE m."Role" = 'user'
      AND m."CreatedAt" >= NOW() - INTERVAL '90 days'
      AND length(trim(m."Content")) BETWEEN 2 AND 200
  ),
  with_next_reply AS (
    SELECT
      u.*,
      next_reply.content    AS next_bot_reply,
      next_reply.is_fallback AS next_bot_was_fallback
    FROM user_msgs u
    LEFT JOIN LATERAL (
      SELECT
        n."Content" AS content,
        -- Heuristic: detect Phase-01 fallback string or generic apologies
        (
          n."Content" ILIKE '%chưa tìm thấy dữ liệu sản phẩm%'
          OR n."Content" ILIKE '%chuyển bạn hỗ trợ%'
          OR n."Content" ILIKE '%em chưa thể xác nhận%'
        ) AS is_fallback
      FROM "ConversationMessages" n
      WHERE n."SessionId" = u.conversation_id
        AND n."Role"      = 'model'
        AND n."CreatedAt" > u.created_at_utc
      ORDER BY n."CreatedAt" ASC
      LIMIT 1
    ) next_reply ON TRUE
  )
  SELECT
    conversation_id,
    tenant_id,
    created_at_utc,
    role,
    content,
    COALESCE(token_count, 0)              AS token_count,
    char_count,
    COALESCE(next_bot_reply, '')          AS next_bot_reply,
    COALESCE(next_bot_was_fallback, FALSE) AS next_bot_was_fallback
  FROM with_next_reply
  ORDER BY
    -- Prioritize fallback rows first (these are the "bot failed" cases we most want to fix)
    next_bot_was_fallback DESC,
    -- Then prefer short queries (Phase 03 cares most about these)
    token_count ASC,
    created_at_utc DESC
) TO STDOUT WITH (FORMAT CSV, HEADER TRUE, FORCE_QUOTE *);
