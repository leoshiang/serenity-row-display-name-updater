#!/usr/bin/env pwsh

Write-Host "正在建置 Serenity Row DisplayName Updater 單一執行檔..." -ForegroundColor Green
Write-Host

# 清理之前的建置
if (Test-Path "dist") {
    Remove-Item -Recurse -Force "dist"
}
New-Item -ItemType Directory -Path "dist" -Force | Out-Null

# 確保我們在專案根目錄
$projectFile = "SerenityRowDisplayNameUpdater.csproj"

if (-not (Test-Path $projectFile)) {
    Write-Host "錯誤: 找不到專案檔案 $projectFile" -ForegroundColor Red
    Write-Host "請確保在專案根目錄下執行此腳本" -ForegroundColor Red
    exit 1
}

# 定義平台清單
$platforms = @{
    "win-x64" = "Windows x64"
    "win-x86" = "Windows x86"  
    "linux-x64" = "Linux x64"
    "osx-x64" = "macOS x64"
    "osx-arm64" = "macOS ARM64"
}

# 建置各平台版本
$successCount = 0
$totalCount = $platforms.Count

foreach ($platform in $platforms.GetEnumerator()) {
    Write-Host "建置 $($platform.Value) 版本..." -ForegroundColor Yellow
    
    $outputPath = "dist/$($platform.Key)"
    
    $publishArgs = @(
        "publish"
        $projectFile
        "-c", "Release"
        "-r", $platform.Key
        "--self-contained", "true"
        "-p:PublishSingleFile=true"
        "-p:PublishTrimmed=true"
        "-o", $outputPath
    )
    
    & dotnet $publishArgs
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ $($platform.Value) 建置成功" -ForegroundColor Green
        $successCount++
    } else {
        Write-Host "✗ $($platform.Value) 建置失敗" -ForegroundColor Red
    }
}

Write-Host
Write-Host "建置結果：$successCount/$totalCount 成功" -ForegroundColor $(if ($successCount -eq $totalCount) { "Green" } else { "Yellow" })
Write-Host

if ($successCount -gt 0) {
    Write-Host "執行檔位於以下目錄：" -ForegroundColor Green
    
    foreach ($platform in $platforms.GetEnumerator()) {
        $outputPath = "dist/$($platform.Key)"
        
        if ($platform.Key.StartsWith("win")) {
            $executablePath = "$outputPath/serupd.exe"
        } else {
            $executablePath = "$outputPath/serupd"
        }
        
        if (Test-Path $executablePath) {
            $size = (Get-Item $executablePath).Length
            $sizeString = if ($size -gt 1MB) { 
                "{0:N1} MB" -f ($size / 1MB) 
            } else { 
                "{0:N0} KB" -f ($size / 1KB) 
            }
            
            Write-Host "  ✓ $($platform.Value): $executablePath ($sizeString)" -ForegroundColor Cyan
        }
    }
    
    Write-Host
    Write-Host "提示：在 Linux/macOS 上執行前，請先設定執行權限：" -ForegroundColor Yellow
    Write-Host "  chmod +x serupd" -ForegroundColor Gray
}