# ZHI Dashboard

ZHI 生态系统的 Web 前端 — 世界管理、实时可视化与模拟控制台。

基于 **React 19** + **HeroUI 3** + **TypeScript** + **Tailwind CSS v4** + **Vite 6**。

## 功能

- **启动页** — 浏览/创建/删除世界，新建时配置全部模拟参数（世界尺寸、食物、温度、战斗、河流、生理）
- **观测仪表盘** — Canvas 实时渲染世界地图、Agent 卡片面板、事件监视器、生态图表、日志
- **系统主题** — 自动跟随系统深色/浅色模式，支持手动切换
- **中英双语** — 完整 i18n 覆盖，支持手动切换中文/英文
- **WebSocket 实时通信** — 世界状态、事件流、日志流

## 快速启动

```bash
npm install
npm run dev        # 开发服务器 → http://localhost:5173
npm run build      # 生产构建 → ../ZHI.Watcher/wwwroot/
npm run typecheck  # TypeScript 类型检查
```

## 项目结构

```
src/
├── main.tsx                         # 应用入口 (I18nProvider + App)
├── index.css                        # 主题变量 (zhi-* 深浅色)
├── app/
│   ├── App.tsx                      # 路由：启动中 → 启动页 → 观测仪表盘
│   ├── LaunchPage.tsx               # 世界列表 + 创建弹窗（含全部配置区段）
│   ├── WorldDashboard.tsx           # 观测仪表盘外壳（3 栏 + 底部面板）
│   └── ControlBar.tsx               # 顶部控制栏（暂停/停止/主题/语言切换）
├── components/
│   ├── ConfigSections.tsx           # ZhiConfig 类型 + 可折叠配置表单
│   ├── LangSwitcher.tsx             # 中/英语言切换按钮
│   ├── ThemeSwitcher.tsx            # 深色/浅色主题切换按钮
│   ├── WorldMap.tsx                 # Canvas 2D 地图渲染
│   ├── AgentCardsPanel.tsx          # Agent 信息卡片侧边栏
│   ├── EventMonitor.tsx             # 实时事件流 + 类型过滤
│   ├── ChartsPanel.tsx              # 生态图表（种群/能量）
│   ├── LogPanel.tsx                 # 服务端日志查看
│   └── SettingsPanel.tsx            # 运行中配置编辑（备用）
├── hooks/
│   ├── useWebSocket.ts              # 世界状态 WebSocket
│   ├── useLogSocket.ts              # 日志 WebSocket
│   ├── useStats.ts                  # 数据库统计查询
│   └── useEcoHistory.ts             # 生态历史时间序列
├── i18n/
│   ├── I18nContext.tsx              # i18n 上下文 + localStorage 持久化
│   └── translations.ts             # 中/英翻译键（150+ 条目）
└── types.ts                         # TypeScript 类型定义
```

## 架构

- **API 代理** — Vite 将 `/api/*` 和 `/ws*` 代理到 `localhost:8088`（ZHI.Watcher 服务端）
- **主题** — CSS 自定义属性 `--zhi-*` 由 `.dark` 类切换，HeroUI `useTheme("system")` 驱动
- **i18n** — `I18nProvider` 包裹根组件，`useT()` hook 返回翻译函数，语言选择持久化到 localStorage
- **性能** — 信息栏与显示切换按钮分离为独立组件，`memo()` 避免 WebSocket 数据 tick 导致的非必要重渲染
