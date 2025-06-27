# IntelliTrans

IntelliTrans 是一个用于处理 .NET IntelliSense文档的翻译工作的命令行工具，可以自动处理 .NET 程序集中的 IntelliSense 文件，并将其翻译成指定语言。

## 功能特性

- **扫描 (Scan)**: 扫描指定目录中的 IntelliSense XML 文件，提取需要翻译的内容并存储到数据库中
- **翻译 (Translate)**: 使用 OpenAI API 对提取的内容进行翻译处理
- **补丁 (Patch)**: 将翻译结果应用到目标文件中，生成本地化的 IntelliSense 文件
- **多数据库支持**: 支持 PostgreSQL 和 SQLite 作为持久化存储
- **灵活配置**: 可配置扫描目录、排除文件、过滤规则等参数

## 系统要求

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 访问 OpenAI API 的密钥（用于翻译功能）
- SQLite（默认数据库）或 PostgreSQL（可选）

## 安装

```bash
# 克隆项目
git clone <repository-url>
cd IntelliTrans

# 恢复依赖项
dotnet restore

# 构建项目
dotnet build
```

## 配置

在使用 IntelliTrans 之前，需要配置 `appsettings.json` 文件：

```json
{
  "DBType": "Sqlite",                    // 数据库类型: "Sqlite" 或 "Postgres"
  "ConnectionStrings": {
    "Sqlite": "Data Source=IntelliTrans.db", // SQLite 连接字符串
    "Postgres": "Host=127.0.0.1;Port=5432;Database=IntelliTrans;User Id=postgres;Password=postgres;"    // PostgreSQL 连接字符串
  },
  "IntelliSense": {
    "IncludeDirs": ["C:\\Program Files\\dotnet\\packs"],  // 要扫描的目录
    "ExcludeFiles": [                    // 要排除的文件
      "Microsoft.VisualBasic.Core.xml",
      "Microsoft.VisualBasic.Forms.xml"
    ]
  },
  "Openai": {
    "ApiKey": "",                        // OpenAI API 密钥
    "Model": "gpt-4o-mini",              // 使用的模型
    "Endpoint": "https://api.openai.com/v1"  // API 端点
  }
}
```

## 使用方法

### 扫描 IntelliSense 文件

```bash
dotnet run -- scan
```

此命令将扫描 [IntelliSense:IncludeDirs](src/IntelliTrans.Cli/appsettings.json) 配置中指定的目录，查找 XML 文件并提取其中的中文内容。

参数：

- `--includeDirs`: 指定要扫描的目录（可选，默认使用配置文件中的值）
- `--excludeFiles`: 指定要排除的文件（可选，默认使用配置文件中的值）
- `--skipNoDll`: 是否跳过没有对应 DLL 文件的 XML 文件（默认: true）
- `--contentFilter`: 内容过滤器正则表达式（默认: `[\u4e00-\u9fa5]`，即匹配中文字符）

### 翻译内容

```bash
dotnet run -- translate
```

此命令将使用 OpenAI API 翻译数据库中提取的内容。

参数：

- `--apiUrl`: OpenAI API 端点（可选，默认使用配置文件中的值）
- `--apiKey`: OpenAI API 密钥（可选，默认使用配置文件中的值）
- `--model`: 使用的模型（可选，默认使用配置文件中的值）
- `--language`: 目标语言（默认: 简体中文）

### 应用补丁

```bash
dotnet run -- patch
```

此命令将翻译后的内容写入新的 IntelliSense XML 文件。

参数：

- `--includeDirs`: 指定要处理的目录（可选，默认使用配置文件中的值）
- `--excludeFiles`: 指定要排除的文件（可选，默认使用配置文件中的值）
- `--skipNoDll`: 是否跳过没有对应 DLL 文件的 XML 文件（默认: true）
- `--savePath`: 保存翻译文件的路径（默认: zh-Hans）
- `--contentFilter`: 内容过滤器正则表达式（默认: `[\u4e00-\u9fa5]`）

## 数据库迁移

项目支持 SQLite 和 PostgreSQL 两种数据库。如果需要切换数据库，请修改 [appsettings.json](src/IntelliTrans.Cli/appsettings.json) 中的 `DBType` 配置，并确保连接字符串正确。

## 项目结构

```
IntelliTrans/
├── src/
│   ├── IntelliTrans.Cli/           # 命令行接口
│   ├── IntelliTrans.Core/          # 核心逻辑
│   ├── IntelliTrans.Database/      # 数据库模型和上下文
│   ├── IntelliTrans.Migrations.Postgres/  # PostgreSQL 迁移
│   └── IntelliTrans.Migrations.Sqlite/    # SQLite 迁移
└── README.md
```
