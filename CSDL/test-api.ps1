# Test API Script for PowerShell

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Testing Nhom1 API Server" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

$baseUrl = "https://localhost:7097"

# Test 1: Health Check
Write-Host "[1] Testing Server Connection..." -ForegroundColor Yellow
try {
    $url = $baseUrl + '/api/homestays?page=1&pageSize=5'
    $response = Invoke-WebRequest -Uri $url -SkipCertificateCheck -ErrorAction Stop
    Write-Host "OK Server is running!" -ForegroundColor Green
    Write-Host "  Status: $($response.StatusCode)" -ForegroundColor Gray
} catch {
    Write-Host "X Server connection failed!" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    exit
}
Write-Host ""

# Test 2: Get Homestays (No Auth Required)
Write-Host "[2] Testing GET /api/homestays..." -ForegroundColor Yellow
try {
    $url = $baseUrl + '/api/homestays?page=1&pageSize=3'
    $response = Invoke-RestMethod -Uri $url -SkipCertificateCheck
    if ($response.success) {
        Write-Host "OK Successfully retrieved homestays!" -ForegroundColor Green
        Write-Host "  Total Count: $($response.data.totalCount)" -ForegroundColor Gray
        Write-Host "  Items in Page: $($response.data.items.Count)" -ForegroundColor Gray
        if ($response.data.items.Count -gt 0) {
            Write-Host "  First Homestay: $($response.data.items[0].name)" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "X Failed to get homestays" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
}
Write-Host ""

# Test 3: Try Login with Admin (if exists)
Write-Host "[3] Testing POST /api/auth/login..." -ForegroundColor Yellow
$loginBody = @{
    email = "admin@homestay.com"
    password = "Admin@123"
} | ConvertTo-Json

try {
    $url = $baseUrl + '/api/auth/login'
    $response = Invoke-RestMethod -Uri $url `
        -Method Post `
        -Body $loginBody `
        -ContentType "application/json" `
        -SkipCertificateCheck
    
    if ($response.success) {
        Write-Host "OK Login successful!" -ForegroundColor Green
        Write-Host "  User: $($response.data.user.fullName)" -ForegroundColor Gray
        Write-Host "  Email: $($response.data.user.email)" -ForegroundColor Gray
        Write-Host "  Token: $($response.data.token.Substring(0,30))..." -ForegroundColor Gray
        $token = $response.data.token
        
        # Test 4: Test authenticated endpoint
        Write-Host ""
        Write-Host "[4] Testing GET /api/homestays/my-homestays (Authenticated)..." -ForegroundColor Yellow
        try {
            $headers = @{
                "Authorization" = "Bearer $token"
            }
            $url = $baseUrl + '/api/homestays/my-homestays'
            $myHomestays = Invoke-RestMethod -Uri $url `
                -Headers $headers `
                -SkipCertificateCheck
            
            if ($myHomestays.success) {
                Write-Host "OK Successfully retrieved user homestays!" -ForegroundColor Green
                Write-Host "  Count: $($myHomestays.data.Count)" -ForegroundColor Gray
            }
        } catch {
            Write-Host "X Failed to get user homestays" -ForegroundColor Red
            Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
        }
    } else {
        Write-Host "X Login failed!" -ForegroundColor Red
        Write-Host "  Message: $($response.message)" -ForegroundColor Gray
    }
} catch {
    Write-Host "X Login request failed" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    Write-Host "  Note: Admin user might not exist yet" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "API Server is ready for Flutter app!" -ForegroundColor Green
Write-Host ""
Write-Host "Swagger UI: $baseUrl" -ForegroundColor Cyan
Write-Host "API Base: $baseUrl/api" -ForegroundColor Cyan
Write-Host ""
