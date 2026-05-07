# Script để reindex tất cả products vào Pinecone
# Sử dụng sau khi update TenantId cho products

$baseUrl = "http://localhost:5030"
$loginUrl = "$baseUrl/admin/login"
$indexUrl = "$baseUrl/admin/api/vector-search/index-all"

# Đọc credentials từ .env
$envFile = Get-Content ".env" -ErrorAction SilentlyContinue
$adminEmail = ($envFile | Select-String "ADMIN_BOOTSTRAP_EMAIL=(.+)").Matches.Groups[1].Value
$adminPassword = ($envFile | Select-String "ADMIN_BOOTSTRAP_PASSWORD=(.+)").Matches.Groups[1].Value

if (-not $adminEmail -or -not $adminPassword) {
    Write-Host "ERROR: Không tìm thấy ADMIN_BOOTSTRAP_EMAIL hoặc ADMIN_BOOTSTRAP_PASSWORD trong .env" -ForegroundColor Red
    exit 1
}

Write-Host "Đang login với admin account..." -ForegroundColor Cyan

# Login để lấy cookie
$loginBody = @{
    email = $adminEmail
    password = $adminPassword
} | ConvertTo-Json

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$loginResponse = Invoke-WebRequest -Uri $loginUrl -Method POST -Body $loginBody -ContentType "application/json" -SessionVariable session -ErrorAction Stop

Write-Host "Login thành công!" -ForegroundColor Green

# Trigger reindex
Write-Host "Đang trigger reindex tất cả products..." -ForegroundColor Cyan

$indexResponse = Invoke-WebRequest -Uri $indexUrl -Method POST -WebSession $session -ErrorAction Stop
$result = $indexResponse.Content | ConvertFrom-Json

if ($result.success) {
    Write-Host "Reindex job đã được khởi động!" -ForegroundColor Green
    Write-Host "Job ID: $($result.jobId)" -ForegroundColor Yellow
    Write-Host "Message: $($result.message)" -ForegroundColor Yellow

    # Poll status
    $statusUrl = "$baseUrl/admin/api/vector-search/index-status/$($result.jobId)"
    Write-Host "`nĐang theo dõi tiến trình..." -ForegroundColor Cyan

    do {
        Start-Sleep -Seconds 2
        $statusResponse = Invoke-WebRequest -Uri $statusUrl -Method GET -WebSession $session -ErrorAction Stop
        $status = $statusResponse.Content | ConvertFrom-Json

        Write-Host "[$($status.status)] $($status.indexedProducts)/$($status.totalProducts) products ($($status.progressPercentage)%)" -ForegroundColor Yellow

        if ($status.currentProductName) {
            Write-Host "  Đang xử lý: $($status.currentProductName)" -ForegroundColor Gray
        }

    } while ($status.status -eq "Running")

    if ($status.status -eq "Completed") {
        Write-Host "`nReindex hoàn tất thành công!" -ForegroundColor Green
        Write-Host "Tổng số products đã index: $($status.indexedProducts)" -ForegroundColor Green
    } else {
        Write-Host "`nReindex thất bại!" -ForegroundColor Red
        Write-Host "Error: $($status.errorMessage)" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Không thể khởi động reindex job!" -ForegroundColor Red
    exit 1
}
