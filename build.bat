@echo off
echo 正在建置 Serenity Row DisplayName Updater 單一執行檔...
echo.

:: 清理之前的建置
if exist "dist" rmdir /s /q "dist"
mkdir "dist"

:: 確保我們在專案根目錄
set PROJECT_FILE=SerenityRowDisplayNameUpdater.csproj

if not exist "%PROJECT_FILE%" (
    echo 錯誤: 找不到專案檔案 %PROJECT_FILE%
    echo 請確保在專案根目錄下執行此腳本
    pause
    exit /b 1
)

:: Windows x64 版本
echo 建置 Windows x64 版本...
dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/win-x64"
if errorlevel 1 (
    echo Windows x64 建置失敗
    goto :error
)

:: Windows x86 版本
echo 建置 Windows x86 版本...
dotnet publish "%PROJECT_FILE%" -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/win-x86"
if errorlevel 1 (
    echo Windows x86 建置失敗
    goto :error
)

:: Linux x64 版本
echo 建置 Linux x64 版本...
dotnet publish "%PROJECT_FILE%" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/linux-x64"
if errorlevel 1 (
    echo Linux x64 建置失敗
    goto :error
)

:: macOS x64 版本
echo 建置 macOS x64 版本...
dotnet publish "%PROJECT_FILE%" -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/osx-x64"
if errorlevel 1 (
    echo macOS x64 建置失敗
    goto :error
)

:: macOS ARM64 版本
echo 建置 macOS ARM64 版本...
dotnet publish "%PROJECT_FILE%" -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/osx-arm64"
if errorlevel 1 (
    echo macOS ARM64 建置失敗
    goto :error
)

echo.
echo 建置完成！執行檔位於以下目錄：
echo   Windows x64: dist/win-x64/serupd.exe
echo   Windows x86: dist/win-x86/serupd.exe
echo   Linux x64:   dist/linux-x64/serupd
echo   macOS x64:   dist/osx-x64/serupd
echo   macOS ARM64: dist/osx-arm64/serupd
echo.

echo 檔案大小：
for /d %%d in (dist\*) do (
    echo   %%~nd:
    for %%f in (%%d\serupd*) do (
        if exist "%%f" (
            echo     %%~nxf: %%~zf bytes
        )
    )
)

echo.
echo 提示：在 Linux/macOS 上執行前，請先設定執行權限：
echo   chmod +x serupd
goto :end

:error
echo.
echo 建置過程中發生錯誤，請檢查輸出訊息

:end
pause