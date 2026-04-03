# Test RAG retrieval for "trị tàn nhang"

$query = "kem trị nám tàn nhang giá bao nhiêu"

Write-Host "Testing RAG with query: $query" -ForegroundColor Cyan
Write-Host ""

# Call RAG endpoint (assuming it exists at /api/admin/test-rag)
$body = @{
    query = $query
    topK = 5
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5030/api/admin/test-rag" -Method Post -Body $body -ContentType "application/json"

    Write-Host "RAG Response:" -ForegroundColor Green
    Write-Host ($response | ConvertTo-Json -Depth 10)
} catch {
    Write-Host "Error calling RAG endpoint: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Trying alternative: Check Pinecone directly via logs" -ForegroundColor Yellow
}
