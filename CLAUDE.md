# 开发注意事项

- 如果你无任何上下文，请读取README.md文档理解这个项目。

- 当你每开发完成一个小模块，则需要提交commit，并且提交commit时不允许增加Co-Author

- 当你每开发完成一个小模块，且该模块更改了主要功能/特性，则你必须时刻更新README.md文档

## 项目方向：人工生命（Artificial Life）

ZHI 已从"智能体沙盒"转向**人工生命**方向。核心原则：

- **不预设文明假设**：无合作、无语言、无社会规则。只有物理、代谢、繁殖、死亡。
- **涌现优先**：复杂行为必须从底层规则中自然出现，不能由程序员硬编码。
- **能量经济**：所有系统通过能量/体力消耗与身体参数耦合，形成自然选择压力。
- **生态位分化**：通过可遗传身体参数（Genome: Size/Speed/Strength/Vision/Fat/ColdResist）创造不同生存策略。

迭代计划见 `plan/ALIFE_ITERATIONS.md`（gitignored）。当前已完成 Iteration 1: Prune & Foundation。

## 架构概要

### 项目结构
- `ZHI.Shared/` — 共享类型与配置（ToolDefinitions, ZhiConfig, Genome, ZhiAction）
- `ZHI.Core/` — ML 核心（GRUBrain: CNN+GRU Actor-Critic, Device）
- `ZHI.Watcher/` — 世界引擎（CosmosEngine 分部类, WebServer, VectorizedState, PPOBuffer）
- `ZHI.Dashboard/` — React + Vite 前端
- `src/ZHI.Tests/` — 单元测试

### 关键常量 (ToolDefinitions.cs)
- StateSize = 340 (294 grid 7×7×6ch + 46 non-grid)
- ActionCount = 8: MoveUp/Down/Left/Right, Eat, Attack, EmitChemical, Drink
- VisionRadius = 3 (7×7 window)
- SignalWaveRadius = 4 (chemical diffusion)

### 神经网络 (GRUBrain.cs)
- CNN: 7×7×6 → Conv→16@5×5 → Conv→32@3×3 → Flatten→288
- GRU: (288+46=334) → 128
- FC: 128 → 64
- Heads: Actor(8) + Chemical(1, sigmoid-gaussian) + Critic(1)
- PPO + GAE, shared brain, all agents batched on GPU

### 状态向量 (Observations.cs)
- [0-293] 7×7×6ch grid (small food, big food, corpse, agent, self, height)
- [294] HP/100, [295] Stress/5, [296] LastAction/7, [297] Age, [298] Stamina/100
- [299] ChemicalMemory, [300-303] Scent N/S/E/W gradient
- [304-306] FoodVisible/5, AgentVisible/8, ScentHere/10
- [307-308] Facing X,Y, [309] ChemicalAge/20
- [310] Hunger/100, [311] Thirst/100, [312] WaterSound/10
- [313] IsEating, [314] IsStationary
- [315-320] Body params (Size/Speed/Strength/Vision/ColdResist mapped to 0-1)
- [321] Height (-10..+10 → 0-1)
- [322-325] ChemicalField N/S/E/W gradient
- [326-339] reserved

### VectorizedState (CPU-side world state)
- Agent arrays: PosX/Y, Existence, Stress, Alive, etc.
- Body params: Genomes[], BodySize[], BodySpeed[], BodyStrength[], BodyVision[], BodyFat[], BodyColdResist[]
- Grid state: FoodTiles, CorpseTiles, ScentGrid, FoodScentGrid, RiverGrid, WaterSoundGrid, TemperatureGrid, ChemicalField[,], TerrainType[,], TerrainTTL[,], RiverFlow[,], DistanceToRiver[,], HeightMap[,]
- Spatial query grids: _agentGrid[], _foodGrid[], _corpseGrid[]

### CosmosEngine 分部类
- `CosmosEngine.cs` — 主循环、推理、PPO 更新
- `Actions.cs` — 动作分发
- `Actions.Tactical.cs` — 具体动作实现（ProcessMove, ProcessEat, ProcessAttack, ProcessEmitChemical, ProcessDrink）
- `Simulation.cs` — 每 tick 模拟（新陈代谢、化学扩散、温度）
- `World.cs` — 世界生成（HeightMap, River, Food, Terrain physics）
- `Lifecycle.cs` — 繁殖、死亡、重生
- `Init.cs` — 初始化
- `Config.cs` — 配置热更新

### 遗传系统
- Genome: 6 个可遗传身体参数，通过 mutation 在繁殖时变异
- 身体参数与能量经济耦合（Size 越大 HP 消耗越快，Speed 越快移动越省力等）
- 繁殖：家长付出 40 HP，子女初始 40 HP，权重交叉 + 变异

## 代码规范

- 命名清晰：使用描述性强的命名，让代码自我解释。
- 简洁性：力求简洁，避免冗余，用最少的代码行数完成功能。
- 一致性：保持项目中命名和编码风格的统一，减少认知负荷。
- 注释：用注释阐明代码意图，但避免过度注释。
- 避免复杂性：将复杂逻辑分解为简单、可管理的函数或模块。
- 重构：定期重构，提升代码的可读性和性能。
- 测试：编写单元测试，确保代码的稳定性和可靠性。
- 错误处理：合理处理错误，增强程序的健壮性。
- 文档：编写清晰的文档，包括 API 文档和项目文档。
- 代码复用：创建可复用的函数或模块，避免重复代码。
- 性能优化：在不牺牲可读性的前提下，优化性能瓶颈。
- 安全性：编写安全的代码，防范常见的安全漏洞。

- 尽可能利用新式语言功能和 C# 版本。
- 避免过时的语言构造。
- 只捕获可以正确处理的异常；避免捕获一般异常。
- 使用特定的异常类型提供有意义的错误消息。
- 使用 LINQ 查询和方法进行集合操作，以提高代码可读性。
- 使用 async 和 await 进行异步编程以处理 I/O 绑定的操作。
- 对数据类型而不是运行时类型使用语言关键字。
- 使用 `int` 而不是无符号类型。
- 仅当读者可以从表达式推断类型时使用 `var`。
- 以简洁明晰的方式编写代码。
- 避免过于复杂和费解的代码逻辑。
