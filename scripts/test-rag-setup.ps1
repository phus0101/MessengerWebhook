#!/usr/bin/env pwsh
# Script test RAG search trực tiếp với Pinecone

$ErrorActionPreference = "Stop"

Write-Host "=== Test RAG Search ===" -ForegroundColor Cyan

# Test 1: Verify Pinecone namespace
Write-Host "`n1. Kiểm tra Pinecone namespace..." -ForegroundColor Yellow
$tenantId = "4dac423d-96ad-44a6-9f33-c78268960c88"
Write-Host "   TenantId: $tenantId" -ForegroundColor Gray

# Test 2: Verify products trong database
Write-Host "`n2. Kiểm tra products trong database..." -ForegroundColor Yellow
$productsQuery = @"
SELECT COUNT(*) as total,
       COUNT(CASE WHEN \"TenantId\" = '$tenantId' THEN 1 END) as with_tenant
FROM \"Products\"
WHERE \"IsActive\" = true;
"@

$result = docker exec -i messenger-postgres psql -U postgres -d messenger_bot -t -c $productsQuery
Write-Host "   $result" -ForegroundColor Gray

# Test 3: Verify embeddings
Write-Host "`n3. Kiểm tra embeddings..." -ForegroundColor Yellow
$embeddingsQuery = @"
SELECT COUNT(*) as total
FROM \"ProductEmbeddings\" pe
JOIN \"Products\" p ON pe.\"ProductId\" = p.\"Id\"
WHERE p.\"TenantId\" = '$tenantId';
"@

$result = docker exec -i messenger-postgres psql -U postgres -d messenger_bot -t -c $embeddingsQuery
Write-Host "   Embeddings: $result" -ForegroundColor Gray

# Test 4: Test products có từ khóa "nám"
Write-Host "`n4. Tìm products có từ khóa 'nám'..." -ForegroundColor Yellow
$searchQuery = @"
SELECT \"Id\", \"Name\", \"Category\"
FROM \"Products\"
WHERE \"IsActive\" = true
  AND \"TenantId\" = '$tenantId'
  AND (\"Name\" ILIKE '%nám%' OR \"Description\" ILIKE '%nám%')
LIMIT 5;
"@

docker exec -i messenger-postgres psql -U postgres -d messenger_bot -c $searchQuery

# Test 5: Kiểm tra conversation session
Write-Host "`n5. Kiểm tra conversation session gần nhất..." -ForegroundColor Yellow
$sessionQuery = @"
SELECT cs.\"Id\", cs.\"FacebookPSID\", cs.\"TenantId\", cs.\"LastActivityAt\",
       (SELECT COUNT(*) FROM \"ConversationMessages\" cm WHERE cm.\"SessionId\" = cs.\"Id\") as msg_count
FROM \"ConversationSessions\" cs
WHERE cs.\"TenantId\" = '$tenantId'
ORDER BY cs.\"LastActivityAt\" DESC
LIMIT 1;
"@

docker exec -i messenger-postgres psql -U postgres -d messenger_bot -c $sessionQuery

Write-Host "`n=== Kết luận ===" -ForegroundColor Cyan
Write-Host "Nếu tất cả checks trên đều OK nhưng RAG vẫn không hoạt động," -ForegroundColor Yellow
Write-Host "vấn đề có thể nằm ở:" -ForegroundColor Yellow
Write-Host "  1. TenantContext không được resolve trong webhook flow" -ForegroundColor White
Write-Host "  2. RAG bị disabled trong config (RAG.Enabled = false)" -ForegroundColor White
Write-Host "  3. Pinecone API key không đúng hoặc expired" -ForegroundColor White
Write-Host "  4. Redis cache trả về kết quả cũ" -ForegroundColor White
Write-Host "`nĐề xuất: Kiểm tra logs của app khi test conversation mới" -ForegroundColor Green
