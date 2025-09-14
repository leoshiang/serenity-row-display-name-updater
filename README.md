# Serenity Row DisplayName Updater

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey.svg)](https://github.com/dotnet/core/blob/main/os-lifecycle-policy.md)

一個專業的自動化工具，用於更新 Serenity Framework 中 Row 類別的 DisplayName 屬性，從資料庫欄位註解中自動同步顯示名稱。

## 使用方法

### 基本語法
```bash
serupd <目錄路徑> [appsettings.json路徑] [--comment-regex <正規表達式>]
```
### 使用範例

**基本用法:**
```bash
# Windows

serupd.exe C:\MyProject\Modules serupd.exe C:\MyProject\Modules C:\MyProject\appsettings.json

# Linux/macOS
./serupd /path/to/project/modules ./serupd /path/to/project/modules /path/to/appsettings.json

```

**進階用法（自訂註解提取）:**

```bash
# 提取註解的第一行（預設行為）
serupd.exe C:\MyProject\Modules --comment-regex "^([^\n\r]+)"

# 提取括號內的文字
serupd.exe C:\MyProject\Modules --comment-regex "(([^)]+))"

# 提取冒號前的文字
serupd.exe C:\MyProject\Modules --comment-regex "^([^:]+)"

# 提取前20個字符
serupd.exe C:\MyProject\Modules --comment-regex "^(.{1,20})"
```
### 參數說明

| 參數 | 說明 | 必填 |
| --- | --- | --- |
| `<目錄路徑>` | 要掃描的 Serenity 專案目錄路徑 | 是 |
| `[appsettings.json路徑]` | 資料庫連接設定檔路徑，預設為目前目錄下的 appsettings.json | 否 |
| `--comment-regex <正規表達式>` | 用於從資料庫註解中提取特定文字的正規表達式 | 否 |

## 功能特點

- **智慧掃描**: 使用 Roslyn 編譯器 API 精確解析 `*Row.cs` 檔案
- **多資料庫支援**: 支援 SQL Server、PostgreSQL、SQLite 資料庫
- **屬性層級更新**: 自動更新或新增屬性的 `DisplayName` 屬性
- **類別層級更新**: 支援更新類別層級的 `DisplayName` 和權限設定
- **註解文字提取**: 支援正規表達式自訂提取註解中的特定部分
- **現代化架構**: 基於 .NET 9.0 和最新 C# 13.0 語法特性
- **高效處理**: 使用 Roslyn 進行精確的程式碼解析和修改
- **安全操作**: 僅更新必要的內容，保持原始程式碼格式
- **單一執行檔**: 可建置為獨立執行檔，無需安裝 .NET Runtime

## 系統需求

- **.NET 9.0** 或更高版本（開發時）
- **支援的資料庫**：
  - SQL Server (Microsoft.Data.SqlClient)
  - PostgreSQL (Npgsql)
  - SQLite (Microsoft.Data.Sqlite)

## 安裝方式

### 方式一：使用預建的單一執行檔（推薦）

從 [Releases](https://github.com/your-repo/serenity-row-display-name-updater/releases) 頁面下載對應平台的 ZIP 檔案：

| 平台 | 檔案名稱 | 說明 |
|------|----------|------|
| Windows 64位元 | `serupd-win-x64.zip` | 支援 Windows 10/11 x64 |
| Windows 32位元 | `serupd-win-x86.zip` | 支援 Windows 10/11 x86 |
| Linux 64位元 | `serupd-linux-x64.zip` | 支援 Linux x64 發行版 |
| macOS Intel | `serupd-osx-x64.zip` | 支援 Intel Mac |
| macOS Apple Silicon | `serupd-osx-arm64.zip` | 支援 M1/M2/M3 Mac |

解壓縮後直接執行，無需安裝 .NET Runtime。

### 方式二：從原始碼建置

#### 快速建置（推薦）

**Windows:**

```bat
# 建置所有平台的單一執行檔並打包成 ZIP
build.bat
````

**Linux/macOS:**

```bash
# 設定執行權限（首次執行）
chmod +x build.sh

# 建置所有平台的單一執行檔並打包成 ZIP
./build.sh
```

#### 手動建置

```bash
# 複製專案
git clone https://github.com/leoshiang/serenity-row-display-name-updater.git
cd serenity-row-display-name-updater

# 一般建置
dotnet build

# 建置單一執行檔（Windows x64）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o dist
```

## 設定檔格式

`appsettings.json` 檔案應包含以下結構：

```json
{
    "Data": {
        "Default": {
            "ConnectionString": "Server=localhost;Database=MyDB;Trusted_Connection=true;",
            "ProviderName": "Microsoft.Data.SqlClient"
        },
        "Postgres": {
            "ConnectionString": "Host=localhost;Database=mydb;Username=user;Password=pass;",
            "ProviderName": "Npgsql"
        },
        "Sqlite": {
            "ConnectionString": "Data Source=mydb.sqlite",
            "ProviderName": "Microsoft.Data.Sqlite"
        }
    }
}
```

### 支援的 ProviderName

| 提供者                     | 資料庫     | 套件版本    |
| -------------------------- | ---------- | ----------- |
| `Microsoft.Data.SqlClient` | SQL Server | 6.1.1       |
| `Npgsql`                   | PostgreSQL | 9.0.3       |
| `Microsoft.Data.Sqlite`    | SQLite     | 10.0.0-rc.1 |

## 註解文字提取功能

工具支援使用正規表達式自訂提取資料庫註解中的特定內容：

### 預設行為

- 自動提取資料庫註解的第一行作為 DisplayName
- 如果註解為多行，只取第一行避免排版問題

### 自訂正規表達式

使用 `--comment-regex` 參數指定提取規則：

| 用途           | 正規表達式           | 說明                  |
| -------------- | -------------------- | --------------------- |
| 第一行（預設） | `^([^\n\r]+)`        | 提取第一行文字        |
| 括號內容       | `\(([^)]+)\)`        | 提取括號內的文字      |
| 冒號前內容     | `^([^:]+)`           | 提取冒號前的文字      |
| 字數限制       | `^(.{1,20})`         | 提取前20個字符        |
| 特定標籤       | `名稱:(.+?)(?:\n|$)` | 提取「名稱:」後的內容 |

### 注意事項

- 如果正規表達式包含捕獲群組 `()`，會使用第一個群組的內容
- 如果沒有捕獲群組，會使用整個匹配結果
- 匹配失敗時會回退到使用註解的第一行
- 語法錯誤時會顯示警告並使用預設行為

## 工作原理

1. **檔案掃描**: 遞迴搜尋目錄中的所有 `*Row.cs` 檔案
2. **Roslyn 解析**: 使用 Microsoft.CodeAnalysis.CSharp 精確解析 C# 語法樹
3. **類別分析**: 提取 Row 類別的 `ConnectionKey` 和 `TableName` 屬性
4. **屬性映射**: 分析屬性與資料庫欄位的 `Column` 對應關係
5. **資料庫查詢**: 根據連接資訊查詢對應資料表的欄位註解
6. **註解處理**: 使用指定的正規表達式提取合適的顯示文字
7. **語法樹更新**: 使用 Roslyn 精確更新語法樹並重新生成程式碼

## 建置腳本說明

專案提供了自動化建置腳本，會自動建置所有平台版本並打包成 ZIP 檔案：

### 自動化功能

- **多平台建置**: 同時建置 Windows、Linux、macOS 版本
- **依賴管理**: 自動下載和設定 7za 壓縮工具
- **檔案清理**: 自動移除 `.pdb` 檔案減少套件大小
- **最佳化壓縮**: 使用最高壓縮等級 (`-mx9`) 減少檔案大小
- **自包含部署**: 包含所有執行時期相依性

### 建置輸出結構

```
dist/
├── serupd-win-x64.zip      # Windows 64位元 (約 15MB)
├── serupd-win-x86.zip      # Windows 32位元 (約 13MB)
├── serupd-linux-x64.zip    # Linux 64位元 (約 16MB)
├── serupd-osx-x64.zip      # macOS Intel (約 16MB)
└── serupd-osx-arm64.zip    # macOS Apple Silicon (約 15MB)
```

## 使用範例

### 基本範例

**原始 Row 類別:**

```c#
[ConnectionKey("Default")]
[TableName("Users")]
public class UserRow : Row<UserRow.RowFields>, IIdRow, INameRow
{
    [Column("user_id")]
    public int? UserId { get; set; }
    
    [Column("user_name")]
    public string UserName { get; set; }

    [Column("email")]
    public string Email { get; set; }

    public class RowFields : RowFieldsBase
    {
        public Int32Field UserId;
        public StringField UserName;
        public StringField Email;
    }
}
```

**資料庫欄位註解:**

- `user_id`: "使用者編號"
- `user_name`: "使用者名稱"
- `email`: "電子郵件地址"

**更新後的程式碼:**

```c#
[ConnectionKey("Default")]
[TableName("Users")]
public class UserRow : Row<UserRow.RowFields>, IIdRow, INameRow
{
    [DisplayName("使用者編號")]
    [Column("user_id")]
    public int? UserId { get; set; }
    
    [DisplayName("使用者名稱")]
    [Column("user_name")]
    public string UserName { get; set; }

    [DisplayName("電子郵件地址")]
    [Column("email")]
    public string Email { get; set; }

    public class RowFields : RowFieldsBase
    {
        public Int32Field UserId;
        public StringField UserName;
        public StringField Email;
    }
}
```

### 複雜註解處理範例

**複雜的資料庫註解:**

```
user_id: "使用者編號
系統自動產生的唯一識別碼
範圍: 1-999999999
不可為空值"

user_name: "使用者名稱 (登入帳號)
長度限制：3-50字元
不可重複
必填欄位"

email: "電子郵件地址
用於接收系統通知
格式驗證：RFC 5322"
```

**不同正規表達式的結果:**

```
# 預設（第一行）
serupd.exe C:\MyProject\Modules
# 結果: "使用者編號", "使用者名稱 (登入帳號)", "電子郵件地址"

# 括號內容
serupd.exe C:\MyProject\Modules --comment-regex "\(([^)]+)\)"
# 結果: "登入帳號" (只有user_name有括號)

# 限制20字元
serupd.exe C:\MyProject\Modules --comment-regex "^(.{1,20})"
# 結果: "使用者編號", "使用者名稱 (登入帳號)", "電子郵件地址"

# 冒號前內容（適合有標籤的註解）
serupd.exe C:\MyProject\Modules --comment-regex "^([^:]+)"
# 結果: "user_id", "user_name", "email"
```

## 開發環境設定

### 開發相依性

**核心套件:**

- 4.14.0 - Roslyn 編譯器 API `Microsoft.CodeAnalysis.CSharp`
- 6.1.1 - SQL Server 連接 `Microsoft.Data.SqlClient`
- `Npgsql` 9.0.3 - PostgreSQL 連接
- 10.0.0-rc.1 - SQLite 連接 `Microsoft.Data.Sqlite`

### IDE 設定

**Visual Studio / Rider:**

```
命令列引數: C:\MyProject\Modules C:\MyProject\appsettings.json --comment-regex "^([^\n\r]+)"
工作目錄: D:\Projects\serupd
```

**VS Code (launch.json):**

```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": ".NET Core Launch",
            "type": "coreclr",
            "request": "launch",
            "program": "${workspaceFolder}/bin/Debug/net9.0/serupd.exe",
            "args": [
                "C:\\MyProject\\Modules",
                "C:\\MyProject\\appsettings.json",
                "--comment-regex", "^([^\\n\\r]+)"
            ],
            "cwd": "${workspaceFolder}",
            "console": "integratedTerminal"
        }
    ]
}
```

## 常見問題

### 建置相關

**Q: 建置腳本執行失敗？**

A: 請檢查：

1. 已安裝 .NET 9.0 SDK
2. Linux/macOS 使用者需執行 `chmod +x build.sh`
3. 網路連線正常（需下載 NuGet 套件和 7za 工具）
4. 磁碟空間充足（至少需要 200MB）

**Q: 7za 工具下載失敗？**

A: 解決方案：

1. 檢查網路連線和防火牆設定
2. 手動下載 7za 並放置在 `tools/` 目錄下
3. 使用企業網路時可能需要設定 Proxy

### 執行相關

**Q: 單一執行檔無法執行？**

A: 故障排除：

1. Linux/macOS: `chmod +x serupd`
2. 確認選擇正確的平台版本
3. 檢查系統版本相容性：   
   - Windows 10 1903+ 或 Windows Server 2019+
   - Linux: glibc 2.23+ 或 musl 1.2.0+
   - macOS 10.15+

**Q: 無法連接資料庫？**

A: 檢查項目：

1. 連接字串格式正確
2. 資料庫服務正在執行
3. 網路連線正常
4. 使用者權限充足
5. 防火牆設定正確

**Q: DisplayName 沒有更新？**

A: 可能原因：

1. 資料庫欄位沒有註解
2. Row 類別的 ConnectionKey 或 TableName 不正確
3. 正規表達式沒有匹配到內容
4. 檔案權限問題（無法寫入）

### 正規表達式相關

**Q: 正規表達式語法錯誤？**

A: 常見問題：

1. 使用單引號而非雙引號包圍
2. 特殊字符未正確轉義
3. 命令列參數解析問題

**正確格式:**

```
# 正確
serupd.exe C:\MyProject\Modules --comment-regex "^([^\n\r]+)"

# 錯誤
serupd.exe C:\MyProject\Modules --comment-regex '^([^\n\r]+)'
```

## 授權條款

本專案採用 MIT 授權條款 - 詳見 [LICENSE](LICENSE) 檔案

## 版本歷史

### v2.0.0 (最新)

- 重構為基於 Roslyn 的語法分析
- 新增 SQLite 資料庫支援
- 支援類別層級 DisplayName 更新
- 升級至 .NET 9.0 和 C# 13.0
- 改進建置腳本和跨平台支援

### v1.0.0

- 初始版本
- 基本 DisplayName 更新功能
- SQL Server 和 PostgreSQL 支援
- 正規表達式註解提取