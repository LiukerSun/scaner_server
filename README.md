# 扫描器服务器 (Scaner Server)

一个使用 .NET WPF 开发的现代化 GUI 应用程序，提供 HTTP 服务器功能来接收、记录和管理 HTTP 请求。支持局域网访问、请求复制、数据库持久化等高级功能。

## ✨ 功能特性

- 🖥️ **现代化 WPF 界面** - 美观的 Material Design 风格界面
- 🌐 **内置 HTTP 服务器** - 基于 ASP.NET Core 的高性能服务器
- 💾 **SQLite 数据库存储** - 自动持久化所有请求记录
- 📊 **实时请求监控** - 实时显示请求头、请求体等详细信息
- 🔄 **服务器控制** - 一键启动/停止服务器
- 📱 **局域网访问** - 自动检测并显示局域网 IP 地址
- 📋 **请求复制功能** - 一键复制请求代码，支持多种编程语言
- 🎯 **智能状态管理** - 复制状态持久化，避免重复复制
- 📈 **请求统计** - 实时显示请求数量统计
- 🔍 **历史记录** - 自动加载历史请求记录
- 🛡️ **错误处理** - 完善的异常处理和用户提示

## 🛠️ 技术栈

- **.NET 8.0** - 最新版本的 .NET 框架
- **WPF** - Windows Presentation Foundation 用于现代化 GUI
- **ASP.NET Core** - 高性能内置 web 服务器
- **Entity Framework Core** - 现代化 ORM 框架
- **SQLite** - 轻量级、高性能数据库
- **Newtonsoft.Json** - 强大的 JSON 处理库

## 📦 安装和运行

### 前置要求

- **Windows 10/11** (64位)
- **.NET 8.0 Runtime** 或 **.NET 8.0 SDK**

### 快速开始

1. **下载项目**
   ```bash
   git clone git@github.com:LiukerSun/scaner_server.git
   cd scaner_server
   ```

2. **运行应用程序**
   ```bash
   dotnet restore
   dotnet build
   dotnet run
   ```

## 🚀 使用指南

### 基本操作

1. **启动服务器**
   - 在端口输入框中设置服务器端口
   - 点击"启动服务器"按钮
   - 服务器启动后会显示局域网访问地址

2. **查看请求记录**
   - 所有 HTTP 请求会自动记录并显示在列表中
   - 显示信息包括：HTTP 方法、路径、请求头、请求体、时间戳、客户端 IP
   - 最多显示 100 条最新记录

3. **复制请求代码**
   - 点击请求记录右侧的"📋"按钮
   - 自动复制该请求的代码到剪贴板
   - 复制后按钮变为"✓"状态，避免重复复制

### 高级功能

- **数据库持久化**: 所有请求记录自动保存到 SQLite 数据库
- **复制状态记忆**: 复制状态会持久化保存，重启应用后仍然有效

## 📁 项目结构

```
ScanerServer/
├── Models/
│   └── HttpRequest.cs                    # HTTP 请求数据模型
├── Data/
│   └── ApplicationDbContext.cs           # Entity Framework 数据库上下文
├── Middleware/
│   └── RequestLoggingMiddleware.cs       # 请求日志中间件
├── Migrations/                           # 数据库迁移文件
├── MainWindow.xaml                       # 主窗口界面设计
├── MainWindow.xaml.cs                    # 主窗口业务逻辑
├── App.xaml                              # 应用程序配置
├── App.xaml.cs                           # 应用程序入口
├── ScanerServer.csproj                   # 项目配置文件
├── scaner_server.db                      # SQLite 数据库文件
└── README.md                             # 项目说明文档
```

## 🗄️ 数据库设计

应用程序使用 SQLite 数据库存储请求记录，数据库文件 `scaner_server.db` 会在首次运行时自动创建。

### 数据表结构

**HttpRequests** - HTTP 请求记录表
| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键，自增 |
| Method | string | HTTP 方法 (GET, POST, PUT, DELETE 等) |
| Path | string | 请求路径 |
| Headers | string | 请求头 (JSON 格式) |
| Body | string | 请求体内容 |
| Timestamp | DateTime | 请求时间戳 |
| ClientIp | string | 客户端 IP 地址 |
| IsCopied | bool | 是否已复制标记 |

## ⚙️ 配置说明


## 🔧 开发说明

### 构建发布版本
```bash
# 发布单文件版本
dotnet publish -c Release -r win-x64 --self-contained true

# 发布依赖框架版本
dotnet publish -c Release -r win-x64 --self-contained false
```

### 添加新的请求处理逻辑
1. 在 `Middleware/RequestLoggingMiddleware.cs` 中添加自定义处理逻辑
2. 在 `Models/HttpRequest.cs` 中扩展数据模型
3. 运行数据库迁移：`dotnet ef migrations add MigrationName`