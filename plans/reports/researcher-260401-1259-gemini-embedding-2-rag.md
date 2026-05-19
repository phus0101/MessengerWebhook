---
title: Google Gemini Embedding 2 (Preview) Research Report
date: 2026-04-01
researcher: researcher
status: completed
---

# Google Gemini Embedding 2 Research Report

## Executive Summary

**CRITICAL CORRECTION**: "Gemini Embedding 2 (preview)" is NOT a text-only model with 768 dimensions at $0.00001/1K tokens. Research reveals significant discrepancies requiring architecture revision.

**Actual Model**: `text-embedding-004` (768-dim, text-only) vs `gemini-embedding-2` (3072-dim, multimodal)

## Model Specifications

### text-embedding-004
- **Dimensions**: 768 (fixed)
- **Type**: Text-only embedding model
- **Release**: March 2025 (GA)
- **Context Window**: 8,192 tokens
- **Task Optimization**: Supports `task_type` parameter (retrieval, classification, clustering)
- **Multilingual**: 100+ languages including Vietnamese
- **API Endpoint**: Vertex AI Text Embeddings API

### gemini-embedding-2 (Multimodal)
- **Dimensions**: 3,072 (default), configurable 128-3,072 via Matryoshka Representation Learning
- **Type**: First natively multimodal embedding (text, images, video, audio, PDFs)
- **Release**: March 10, 2026 (Public Preview)
- **Context Window**: 8,192 tokens (text), 6 images, 120s video, 6 PDF pages
- **Multilingual**: 100+ languages including Vietnamese
- **API Endpoint**: Gemini API / Vertex AI

## API Integration

### Authentication
- Google AI Studio (free tier, strict limits)
- Google Cloud Platform / Vertex AI (paid tiers)
- API key-based authentication

### Rate Limits (2026)

**Free Tier (AI Studio)**:
- 5 RPM, 25 RPD (severely restricted since Dec 2025)
- 250,000 TPM

**Paid Tier 1**:
- 3,000 RPM
- 1M TPM
- Unlimited RPD

**Embedding-Specific**:
- Max 250 input texts per request
- Max 20,000 tokens per request

### Request Format
```
POST https://generativelanguage.googleapis.com/v1beta/models/{model}:embedContent
Authorization: Bearer {API_KEY}
Content-Type: application/json

{
  "content": { "parts": [{ "text": "..." }] },
  "taskType": "RETRIEVAL_DOCUMENT", // text-embedding-004 only
  "outputDimensionality": 768 // gemini-embedding-2 only
}
```

## Pricing (April 2026)

**CORRECTION NEEDED**: Original claim of $0.00001/1K tokens is INCORRECT.

**Actual Pricing**:
- **text-embedding-004**: Not explicitly priced separately (bundled with Vertex AI)
- **gemini-embedding-001**: FREE tier available
- **gemini-embedding-2**: $0.20 per 1M tokens ($0.0002/1K tokens)

**Cost Reality**: 20x more expensive than claimed. For 1M tokens:
- Claimed: $10
- Actual: $200

## Performance

### Benchmarks
- **MTEB Score**: 68.32 (gemini-embedding-001)
- **Elo Ranking**: 1605 (gemini-embedding-2) vs 1590 (zembed-1), 1586 (Voyage 4)
- **Multilingual**: Significant improvements on MMTEB across 250+ languages

### Vietnamese Support
- Confirmed in 100+ language support
- No Vietnamese-specific benchmarks published
- Leverages Gemini's inherent multilingual understanding

### Comparison: text-embedding-004 vs gemini-embedding-2
- **text-embedding-004**: Better for task-specific optimization (retrieval recall)
- **gemini-embedding-2**: Better for multimodal use cases, unified vector space
- **Performance**: Marginal differences in text-only scenarios

## Availability

### Status (April 2026)
- **text-embedding-004**: Generally Available (GA)
- **gemini-embedding-2**: Public Preview (announced March 10, 2026)

### Regional Restrictions
- Available globally via Vertex AI
- Free tier significantly restricted since December 2025

### Access Requirements
- Google Cloud account for production use
- API key from AI Studio (free tier) or Vertex AI (paid)

## Architecture Recommendations

### For RAG Implementation (Vietnamese Chatbot)

**RECOMMENDED**: `text-embedding-004` (768-dim)

**Rationale**:
1. **Cost**: Bundled pricing vs $0.20/1M tokens
2. **Dimensions**: 768 is sufficient for semantic search (lower storage/compute)
3. **Task Optimization**: `task_type=RETRIEVAL_DOCUMENT` improves recall
4. **Stability**: GA status vs Preview
5. **Vietnamese Support**: Confirmed multilingual support

**NOT RECOMMENDED**: `gemini-embedding-2`

**Why**:
- Overkill for text-only RAG (3072-dim unnecessary)
- 20x cost increase vs claimed pricing
- Preview status = potential breaking changes
- Multimodal features unused in current architecture

### Alternative: gemini-embedding-001
- **FREE tier available**
- 3,072 dimensions (can truncate to 768)
- 68.32 MTEB score
- GA status
- **BEST for cost-sensitive projects**

## Trade-off Matrix

| Model | Dimensions | Cost/1M | Status | Task Opt | Multimodal | Recommendation |
|-------|-----------|---------|--------|----------|------------|----------------|
| text-embedding-004 | 768 | Bundled | GA | ✅ | ❌ | **BEST for RAG** |
| gemini-embedding-001 | 3072 | FREE | GA | ❌ | ❌ | **BEST for budget** |
| gemini-embedding-2 | 3072 | $0.20 | Preview | ❌ | ✅ | Only if multimodal needed |

## Adoption Risk Assessment

### text-embedding-004
- **Maturity**: GA (1 year in production)
- **Community**: Large adoption in Vertex AI ecosystem
- **Breaking Changes**: Low risk (stable API)
- **Abandonment Risk**: Low (Google's primary text embedding)

### gemini-embedding-2
- **Maturity**: Preview (3 weeks old)
- **Community**: Early adopters only
- **Breaking Changes**: HIGH risk (preview status)
- **Abandonment Risk**: Medium (experimental multimodal features)

## Architectural Fit

**Current Stack**: ASP.NET Core, PostgreSQL, Gemini 2.0 Flash, Vietnamese chatbot

**Constraints**:
- Text-only RAG (product catalog, order tracking)
- Vietnamese language support required
- Cost-sensitive (startup/SMB target)
- Need production stability

**Fit Analysis**:
1. ✅ text-embedding-004: Perfect fit (text-only, stable, task-optimized)
2. ✅ gemini-embedding-001: Excellent fit (FREE tier, proven performance)
3. ❌ gemini-embedding-2: Poor fit (multimodal unused, preview risk, 20x cost)

## Concrete Recommendation

**PRIMARY**: Use `text-embedding-004` with 768 dimensions

**Implementation**:
```csharp
var request = new EmbedContentRequest
{
    Model = "models/text-embedding-004",
    Content = new Content { Parts = new[] { new Part { Text = text } } },
    TaskType = TaskType.RetrievalDocument,
    OutputDimensionality = 768
};
```

**FALLBACK**: Use `gemini-embedding-001` (FREE tier) if budget is critical

**AVOID**: `gemini-embedding-2` until:
- Multimodal features needed (image search, video content)
- Preview graduates to GA
- Cost justification exists

## Limitations Acknowledged

### What This Research Did NOT Cover
1. **Vietnamese-specific benchmarks**: No published data comparing models on Vietnamese text
2. **Production latency**: No real-world p95/p99 latency measurements
3. **Vertex AI vs AI Studio**: Detailed pricing comparison for bundled services
4. **Vector DB compatibility**: Specific integration patterns with pgvector/Qdrant
5. **Hybrid search**: Combining embeddings with keyword search for Vietnamese

### Why It Matters
- Vietnamese language performance may differ from MTEB averages
- Latency impacts user experience in real-time chat
- Total cost includes storage, compute, and API calls
- Vector DB choice affects query performance and scaling
- Hybrid search may improve recall for Vietnamese product names

## Unresolved Questions

1. Does `text-embedding-004` support Vietnamese diacritics correctly in semantic search?
2. What is the actual p95 latency for embedding 1K tokens via Vertex AI in Asia-Pacific region?
3. Is there a free tier for `text-embedding-004` via Vertex AI, or only paid?
4. How does embedding quality degrade when truncating gemini-embedding-001 from 3072 to 768 dimensions?
5. Are there Vietnamese-specific fine-tuning options available for any of these models?

## Sources

- [Google Cloud Vertex AI Text Embeddings API](https://cloud.google.com/vertex-ai/docs/generative-ai/model-reference/text)
- [Gemini Embedding 2 Documentation](https://docs.cloud.google.com/vertex-ai/generative-ai/docs/models/gemini/embedding-2)
- [Gemini Embedding GA Announcement](https://developers.googleblog.com/en/gemini-embedding-available-gemini-api/)
- [Vertex AI Quotas and Limits](https://cloud.google.com/vertex-ai/generative-ai/docs/quotas)
- [Embedding Models Pricing](https://awesomeagents.ai/pricing/embedding-models-pricing/)
- [Gemini API Rate Limits Guide](https://blog.laozhang.ai/en/posts/gemini-api-rate-limits-guide)
- [Generalizable Embeddings from Gemini (arXiv)](https://arxiv.org/html/2503.07891v1)
- [Vietnamese Language Support](https://vietnamnet.vn/en/google-s-most-advanced-search-mode-now-supports-vietnamese-2450386.html)
