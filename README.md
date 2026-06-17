# ZHI (栀) — 空间化多智能体生态系统

## 概述

ZHI 是一个强化学习驱动的**具身多智能体**人工生命模拟。16 个数字生命体在 20×20 网格世界中，通过局部观察（5×5 视野）和 7 个动作学会生存、捕食、攻击、通信。

核心哲学：**所有复杂行为必须从简单规则中涌现，程序员不预设任何社会关系。**

没有"友方"、没有"语言含义"、没有预设合作。如果合作出现，那是因为它有生存优势。

---

## 世界规则

- **20×20 网格**，agent 有 (x, y) 坐标
- **5×5 局部视野** — 无全局信息，GRU 记忆补偿
- **气味系统** — agent 移动留下 scent trail，每 tick 衰减 ×0.95
- **食物** — 最多 30 个，每 tick 5% 概率生成，TTL=100 tick 后腐烂
- **Stress 战斗** — 攻击叠加 Stress，Stress 每 tick 持续扣血

### 动作空间 (7 actions)

| 动作 | 效果 |
|------|------|
| Move ×4 | 上下左右移动，碰壁不动，留气味 |
| Eat | 吃脚下食物 +10 Existence，失败 -1 |
| Attack | Manhattan≤1 最近敌人 Stress += 0.5，自身 -1 |
| Signal | 广播 signal_value (0~3) 到 5×5 范围，自身 -2 |

Signal 是**双头决策**：Actor 输出 7 个动作 logits + 4 个信号 logits。Agent 自己学"什么时候说"和"说什么"。

### 观察向量 (37 维)

```
[0-24]   5×5 局部网格编码 (空/食物/agent/自己)
[25-29]  自身状态 (existence, stress, last_action, tick_alive, last_signal)
[30-33]  气味梯度 (北/南/东/西方向差)
[34-36]  局部统计 (可见食物数, 可见agent数, 当前格气味)
```

### 奖励 (极简)

```
+0.1    存活
+3.0    吃到食物
-1.0    Eat 失败
-20.0   死亡
```

不奖励攻击，不奖励通信。攻击是否有价值，让 agent 自己发现。

---

## 训练架构

- **GRU(37→128)** → Linear(128→64) → Actor(64→7) + Signal(64→4) / Critic(64→1)
- **PPO + GAE** (64 steps rollout, 4 epochs)
- **RND** intrinsic curiosity (鼓励探索未见区域)
- **遗传算法** — 全灭后按 fitness 选择，MAP-Elites 行为多样性
- **16 agents** 批量推理，CUDA tensors

---

## 技术栈

| 组件 | 技术 |
|------|------|
| 运行时 | .NET 10 |
| 神经网络 | TorchSharp 0.107.0 + CUDA |
| 持久化 | SQLite |
| 前端 | React 19 + TypeScript + TailwindCSS |
| 构建 | Vite 8 |

---

## 项目结构

```
ZHI/
├── src/
│   ├── ZHI.Shared/          # 共享定义
│   │   ├── ToolDefinitions.cs   # 动作枚举, StateSize=37
│   │   └── Config.cs            # 配置模型
│   ├── ZHI.Core/            # 神经网络库
│   │   ├── GRUBrain.cs          # 双头 Actor-Critic + PPO
│   │   ├── RNDModule.cs         # Random Network Distillation
│   │   └── Device.cs            # CUDA 设备管理
│   ├── ZHI.Watcher/         # 宇宙引擎
│   │   ├── CosmosEngine.cs      # 网格 tick 逻辑 + 遗传算法
│   │   ├── VectorizedState.cs   # 空间状态 + 37维观察组装
│   │   ├── PPOBuffer.cs         # Rollout 缓冲区
│   │   ├── MAPElitesGrid.cs     # 行为多样性网格
│   │   ├── Blackbox.cs          # SQLite 死亡记录
│   │   └── WebServer.cs         # HTTP + WebSocket
│   └── ZHI.Dashboard/       # React 前端
│       └── src/
│           ├── App.tsx
│           ├── components/WorldMap.tsx  # Canvas 20×20 地图
│           └── hooks/useWebSocket.ts
└── ZHI.slnx
```

---

## 运行

```bash
# 编译
dotnet build

# 启动
dotnet run --project src/ZHI.Watcher

# 浏览器 → http://localhost:8088

# 前端开发模式
cd src/ZHI.Dashboard && npm install && npm run dev
```

---

## 演化路线图

- **V2 (当前)**: 空间网格 + 局部观察 + 气味 + Stress 战斗 + Signal 涌现
- **V3 (计划)**: 自然繁殖 (Existence>80 → split)，遗传特征，社会结构涌现
