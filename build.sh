#!/bin/bash

echo "正在建置 Serenity Row DisplayName Updater 單一執行檔..."
echo

# 清理之前的建置
if [ -d "dist" ]; then
    rm -rf "dist"
fi
mkdir -p "dist"

# 確保我們在專案根目錄
PROJECT_FILE="SerenityRowDisplayNameUpdater.csproj"

if [ ! -f "$PROJECT_FILE" ]; then
    echo "錯誤: 找不到專案檔案 $PROJECT_FILE"
    echo "請確保在專案根目錄下執行此腳本"
    exit 1
fi

# 建置函數
build_platform() {
    local platform=$1
    local platform_name=$2
    
    echo "建置 $platform_name 版本..."
    
    if dotnet publish "$PROJECT_FILE" -c Release -r "$platform" --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o "dist/$platform"; then
        echo "✓ $platform_name 建置成功"
    else
        echo "✗ $platform_name 建置失敗"
        return 1
    fi
}

# 建置各平台版本
build_platform "win-x64" "Windows x64"
build_platform "win-x86" "Windows x86"
build_platform "linux-x64" "Linux x64"
build_platform "osx-x64" "macOS x64"
build_platform "osx-arm64" "macOS ARM64"

echo
echo "建置完成！執行檔位於以下目錄："
echo "  Windows x64: dist/win-x64/serupd.exe"
echo "  Windows x86: dist/win-x86/serupd.exe"
echo "  Linux x64:   dist/linux-x64/serupd"
echo "  macOS x64:   dist/osx-x64/serupd"
echo "  macOS ARM64: dist/osx-arm64/serupd"
echo

echo "檔案大小："
for dir in dist/*/; do
    if [ -d "$dir" ]; then
        platform=$(basename "$dir")
        echo "  $platform:"
        for file in "$dir"serupd*; do
            if [ -f "$file" ]; then
                size=$(ls -lh "$file" | awk '{print $5}')
                filename=$(basename "$file")
                echo "    $filename: $size"
            fi
        done
    fi
done

echo
echo "提示：在 Linux/macOS 上執行前，請先設定執行權限："
echo "  chmod +x serupd"