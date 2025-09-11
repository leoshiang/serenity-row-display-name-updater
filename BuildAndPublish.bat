@echo off
echo 正在建置當前平台的單一執行檔...
echo.

:: 確保我們在專案根目錄
set PROJECT_FILE=SerenityRowDisplayNameUpdater.csproj

if not exist "%PROJECT_FILE%" (
    echo 錯誤: 找不到專案檔案 %PROJECT_FILE%
    echo 請確保在專案根目錄下執行此腳本
    pause
    exit /b 1
)

:: 清理之前的建置
if exist "serupd.exe" del "serupd.exe"
if exist "serupd.pdb" del "serupd.pdb"

:: 建置當前平台版本 (Windows x64)
echo 建置 Windows x64 版本...
dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "."

echo.
if exist "serupd.exe" (
    echo 建置完成！執行檔：serupd.exe
    echo.
    for %%f in (serupd.exe) do echo 檔案大小：%%~zf bytes
    echo.
    echo 現在可以執行：
    echo serupd.exe ^<目錄路徑^> [appsettings.json路徑]
) else (
    echo 建置失敗！
)

pause