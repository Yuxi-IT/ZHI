# ZHI (栀) — Zero-Hypothesis Intelligence Ecosystem

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13-239120?logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![TorchSharp](https://img.shields.io/badge/TorchSharp-0.107-EE4C2C?logo=pytorch)](https://github.com/dotnet/TorchSharp)
[![React](https://img.shields.io/badge/React-19-61DAFB?logo=react)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6?logo=typescript)](https://www.typescriptlang.org/)
[![Three.js](https://img.shields.io/badge/Three.js-r174-000000?logo=threedotjs)](https://threejs.org/)
[![SQLite](https://img.shields.io/badge/SQLite-3-003B57?logo=sqlite)](https://www.sqlite.org/)
[![PPO](https://img.shields.io/badge/RL-PPO%2BGAE-blueviolet)](https://arxiv.org/abs/1707.06347)
[![Artificial Life](https://img.shields.io/badge/field-Artificial%20Life-brightgreen)](https://en.wikipedia.org/wiki/Artificial_life)

[中文文档](README.md) | English

---

## What We're Asking

A fundamental question:

> If there are no preset social rules, no "cooperate" instruction, no definition of "language" — only survival pressure, finite resources, local perception, and action capability — can complex behavior emerge from scratch?

ZHI is a **minimal digital ecosystem**, attempting to observe the most complex results from the simplest rules.

---

## Design Philosophy: Presuppose Nothing

There are no friends, enemies, factions, social relationships, language semantics, or cooperation rules in the system.

We define only seven things:

- **Survival** — A unified energy currency (Energy) drives everything: all active actions (move/attack/signal) deduct directly from Energy. Energy can only be obtained through eating — no passive recovery. Energy decays every tick (base 0.1/tick + corpse pollution + aging + cold metabolism acceleration + dehydration penalty). Water decays independently (0.1/tick), accelerated by heat, restored by drinking. Energy ≤ 0 → death. **Lifecycle**: Agents pass through juvenile stage (first N ticks: narrow vision/slow speed/high metabolism) → adult (normal capability) → senescence. **Pregnancy system**: Reproduction uses a gestational mechanism — the parent enters pregnancy (lasts N ticks, movement speed halved), giving birth when complete. Pregnant agents take 1.4× damage; birth carries a mortality risk (BirthRisk).
- **Resources** — Closed ecological loop: plants have a complete four-stage life cycle (Seed → Sprout → Adult → Decay). Seeds lie dormant until conditions are right (nutrient > 0.4 + groundwater > 0.25 + sunlight + suitable temperature); sprouts grow fast but are fragile (narrow environmental tolerance); adults can spread seeds via wind (at an energy cost); decaying plants slowly return nutrients to the soil. Edibility varies by stage (Sprout 60% / Adult 100% / Decay 30%); Seeds are inedible and emit no food scent. Agents eat plants for energy; dead agents decay and return nutrients. Plant growth follows a temperature bell curve (optimal 25°C), influenced by sunlight, groundwater, and nutrients; extreme temperatures (< -2°C / > 45°C) are lethal. Corpse decay → nutrient diffusion → plant growth → agent consumption → death & decay — a fully closed material-energy cycle. **Plant Lifecycle v4.7**: Four-stage state machine replaces the single-state energy model; Seed/Sprout/Adult/Decay each have independent growth/survival/reproduction rules, with stage transitions triggered by environmental conditions.
- **Space** — 64×64 grid, movement has a cost; max 2 agents per cell (exceeding blocks movement), creating natural spatial competition. Random rivers cross the map (5 cells wide: shallow water is traversable, deep water is impassable), with random fords (crossable shallow crossings). Rivers regenerate each generation.
- **Environment** — 24-hour day/night cycle (12 real minutes = 1 day); sinusoidal temperature (5°C at dawn ~ 35°C in the afternoon). **Natural disasters**: Randomly triggered floods (rain doubled at high humidity + heavy rain every 10 ticks), droughts (accelerated evaporation), heat waves (temperature +N°C), and cold snaps (temperature -N°C), each lasting a configurable number of ticks. Agents have a body temperature property (inertially tracking ambient temperature), emit body heat (own cell +2°C × HP ratio, 8 neighbors +1°C × HP ratio), with heat dropping when injured. Rivers cool surroundings. Low body temperature (< 15°C) triggers extra HP decay; huddling slows heat loss. High temperature (> 30°C) accelerates thirst. Heatmap overlay + per-cell temperature tooltip on the dashboard. **Wind & Pressure v4.5**: Temperature differences produce pressure gradients (warm = low, cold = high); pressure gradients generate wind (2D vector field). Wind drives temperature advection (semi-Lagrangian), long-range scent transport, directional seed dispersal, and accelerated evaporation. Dashboard overlays include pressure heatmap and wind arrows. **Dynamic Water Cycle v4.4**: Random rainfall (humidity-driven intensity with circular falloff); surface water flows along elevation gradients (steep slope runoff accelerated × SlopeRunoffMult); rivers act as drains (surface water entering rivers is removed from the system; rivers self-drain faster); daytime evaporation (positively correlated with temperature and wind speed); surface water ↔ groundwater exchange (soil permeability derived from terrain: high near rivers via alluvial deposits, low at high elevations with thin soil, low on steep slopes); groundwater 4-neighbor diffusion. **Wet/Dry Season Rhythm**: ~10,000 tick sin-wave-driven humidity oscillation — dry seasons expand temperature swings by 1.3× with less rain; wet seasons narrow swings with more rain and flourishing plants. **Biomes & Sunlight v4.6**: Environment-derived biome system — six-dimensional classification (Height, Groundwater, SurfaceWater, Nutrient, Temperature, DistanceToRiver) into 8 biomes (Water / River Bank / Desert / Grassland / Jungle / Wetland / Highland / Valley). Each biome acts as a multiplier on evaporation rate, agent body-temperature change rate, and plant growth rate. Sunlight system: solar elevation sin curve (noon peak × aspect correction — south-facing bonus ×1.5, north-facing penalty, northern-hemisphere assumption) × highland extra illumination. Sunlight heats the ground temperature grid (SolarHeatingRate), accelerates water evaporation (SunEvaporationMult), and boosts plant photosynthesis (SunPhotosynthesisBoost = 2.0×). Dashboard provides a biome color overlay (8 colors); hover shows biome name, temperature, and soil parameters.
- **Perception** — 7×7 directional vision (a cone-shaped area in the agent's facing direction; rear is invisible); no global map. Water flow sound propagates 10 cells (Manhattan distance, per-cell decay 0.9). Scent and food scent are advected by wind, forming long-range chemical cues. **Perception noise**: Plant observations have a MissChance probability of false negatives; chemical memory has multiplicative noise (±NoiseChemicalRange); water sound has Gaussian noise (σ = NoiseSoundStd) — agents receive imperfect information and must learn to decide under uncertainty. **Short-term memory**: 4-channel decaying markers (saw_food / was_attacked / ate / drank), decaying ×0.95 per tick, set to 1 on event trigger, fed as additional observation input to the neural network.
- **Needs** — Unified metabolism: two axes — Energy and Water. Energy is consumed every tick (base metabolism + aging + corpse pollution + cold metabolism acceleration + dehydration penalty). Water decays every tick (base + heat acceleration). Energy ≤ 0 → death. Metabolic cascade: cold → metabolism accelerates → energy drain increases → death; heat → water loss → dehydration → energy penalty.
- **Actions** — 8 discrete actions: Move Up/Down/Left/Right (flat land 0.5 Energy × BodySpeed multiplier; shallow water +1.0; deep water +2.5; climbing out of deep water +1.0), Eat (extract food energy per tick; multiple agents can compete for the same food simultaneously), Attack (4-tick cooldown, +0.5 Stress, costs 8 Energy / BodyStrength, damage × BodyStrength multiplier), Emit Chemical (continuous value 0–1, sigmoid-gaussian sampling, costs 3 Energy, radius-4 Chebyshev diffusion into the ChemicalField, simultaneously broadcasts to nearby agents' ChemicalMemory), Drink (must be near a water source, +40 Water).

That's it. Everything else — if it appears — is emergent.

The chemical pheromone system is the best example. Agents can emit continuous-strength (0–1) chemical pheromones (sigmoid-gaussian sampling, costs 3 Energy; emission is blocked at low energy). Pheromones diffuse in a wave centered on the agent — radius 4 (9×9 area), Chebyshev distance linear decay. After a single pulse, the signal persists through 4-neighbor diffusion + 0.95/tick natural decay, forming a lasting chemical field. Emission strength is boosted by BodyFat (more fat = stronger emission). The system defines no semantics for the pheromone — agents must learn for themselves when, where, and at what strength to emit. If a stable emission pattern emerges, it's because it confers a survival advantage, not because a programmer wrote a meaning table.

---

## Why "Minimal"

We deliberately keep the system as small as possible:

Only **64 agents** (population cap, configurable). A small population means each individual's life or death affects the group; resource competition is real; there is no "average survival under the law of large numbers."

**340-dimensional observation vector**. What an agent sees:

- [0–293] 7×7 directional grid encoding (49 cells × 6 channels: plant energy / groundwater saturation / corpse / other agent / self / terrain). The visible region is determined by agent facing (cone-shaped forward area visible, rear invisible). CNN extracts spatial features (pure convolution, no pooling: 6ch → Conv(k=5,p=0) → 16@3×3 → Conv(k=3,p=0) → 32@1×1), compressed into a 288-dim feature vector.
- [294] Energy / 100, [295] Stress / 5, [296] LastAction / 7, [297] Age, [298] reserved
- [299] Chemical memory (continuous single value, decaying ×0.95 per tick)
- [300–303] Agent scent gradient (N/S/E/W neighbor-difference)
- [304–306] Local stats (visible food / 5, visible agents / 8, current-cell scent / 10)
- [307–308] Facing vector (facing_x, facing_y)
- [309] Chemical freshness (ChemicalAge / 20)
- [310] Water / 100, [311] reserved, [312] water sound / 10
- [313] IsEating, [314] IsStationary
- [315–319] Body parameters (Size 0.3–2.5 → 0–1, Speed 0.3–2.5 → 0–1, Strength 0.3–2.5 → 0–1, VisionRange 0.3–2.5 → 0–1, Fat 0–1)
- [320] ColdResist 0–1
- [321] Elevation (−10..+10 → 0–1)
- [322–325] Chemical field gradient (N/S/E/W neighbor-difference)
- [326] HeatResist 0–1
- [327–330] Short-term memory (saw_food / was_attacked / ate / drank, decaying ×0.95 per tick)
- [331–339] reserved

**CNN + GRU hybrid architecture**: The 7×7×6 directional grid passes through two pure convolutional layers (6ch → Conv(k=5,p=0) → 3×3×16 → ReLU → Conv(k=3,p=0) → 1×1×32 → ReLU), flattening to 288 dims; then concatenated with the remaining 46 non-grid observation values into a 334-dim vector, fed into GRU(334→128) → FC(128→64) → Actor(8) / Chemical(1) / Critic(1) three heads. The Chemical output uses sigmoid + fixed std 0.15 Gaussian sampling to produce a continuous emission strength.

**Heritable body parameters (Genome)**: Each agent has 7 heritable parameters — Size (body size 0.3–2.5, affects energy consumption rate), Speed (0.3–2.5, reduces movement energy cost), Strength (0.3–2.5, increases attack damage), VisionRange (0.3–2.5), FatStorage (fat 0–1, boosts pheromone emission), ColdResistance (cold resistance 0–1, reduces cold damage), HeatResistance (heat resistance 0–1, reduces high-temperature water loss). All parameters are coupled through the energy economy: larger Size means faster per-tick Energy consumption; higher Speed means cheaper movement. Parameters mutate during reproduction (Gaussian noise, std = 0.05) and are passed to offspring, creating heritable niche differentiation.

**Terrain & Elevation**: The world generates a continuous elevation map (4-octave value noise, −10 to +10). Rivers follow elevation gradients, naturally flowing to low ground. Elevation effects: pits narrow vision and reduce attack damage but provide warmth at night; high ground expands vision and increases attack damage. Elevation serves as one of the agent's perception channels, providing a foundation for future "high-ground / low-ground" strategy differentiation.

**8 actions**, not a continuous control space. Move Up/Down/Left/Right, Eat, Attack, Emit Chemical, Drink. Complex behavior must emerge from combinations of these atomic operations. All civilization-presuming actions (push, pull, terrain modification) have been removed, reducing the action space to the most fundamental survival operations.

**Minimal reward**: Only survival and eating are rewarded. No reward for attacking, cooperating, or communicating. Whether attacking is valuable, cooperation is beneficial, or signals are useful — the agents must discover this entirely on their own.

**Unified metabolism**: Energy and Water are two axes. Energy is the unified currency — all active actions (move/attack/signal) deduct directly from Energy, with no passive recovery; only eating provides energy. Water decays independently; heat accelerates water loss; drinking restores it. Metabolic cascade: cold → metabolism accelerates → energy drain increases → death; heat → water loss → dehydration → energy penalty. This ensures PPO must learn to balance the economics of movement, attack, foraging, and drinking.

**Day/Night & Temperature**: 24-hour day/night cycle (12 real minutes = 1 day = 3,600 ticks). Sinusoidal temperature — coldest 5°C at dawn (04:00), hottest 35°C in the afternoon (14:00).

- **Agent body heat**: Each living agent emits trace body heat (own cell +0.3°C, 8 neighboring cells +0.15°C each), scaled by HP ratio. Stationary agents emit extra heat (own cell +0.5°C, neighbors +0.3°C). Large gatherings form perceptible microclimates — 10 stationary agents huddling together can raise the core cell temperature by ~8°C.

- **Thermal physics v4.3**: Persistent temperature grid (incremental updates rather than overwrites), with thermal diffusion conduction and high water heat capacity. Water target temperature is 14°C below air temperature (`WaterCoolingOffset = 14`), ensuring rivers stay cool year-round. Water temperature change rate is only 1/4 of land (`WaterHeatCapacity = 4.0`), producing thermal inertia. Deep water is an additional 3°C colder. Heat diffuses via 4-neighbor conduction (`ThermalDiffusionRate = 0.12`); cold and warmth permeate from rivers into the land. A precomputed distance-to-river field (`DistanceToRiver`) makes land temperature change rate transition continuously with river proximity (`RiverLandInfluence = 8` cell-range exponential decay), forming a natural river microclimate gradient. **Elevation temperature lapse**: Per-cell temperature changes linearly with elevation (`HeightLapseRate = 0.5`, i.e. highlands are ~10°C colder than lowlands), creating natural temperature stratification from warm valleys to cold mountains. **Agent body temperature**: Each agent has an independent body-temperature property, approaching ambient temperature at 0.05/tick (2× acceleration in water: 0.1/tick), with a hard floor of 26°C. **Hypothermia system**: Body temperature below 33°C begins linearly deducting Energy (max 0.08 Energy/tick at 26°C, ~4-minute lethal window). In extreme deep-water night scenarios the effective temperature can drop to −3°C; body temperature quickly falls to 26°C, triggering maximum hypothermia damage — the agent must reach shore. Daytime heat (> 30°C) accelerates Water decay (up to 1.5×), pushing agents toward water sources. The Dashboard map provides a temperature heatmap overlay (blue → green → orange → red); when enabled, hovering over any cell shows the exact temperature, making heat-island effects and river cold-belts visually observable.

- **Rivers & Deep Water**: 5-cell-wide random rivers, with 1 central deep-water cell (traversable but high energy cost) and shallow water on both sides (traversable). Deep-water movement costs an extra 2.5 Energy (shallow water +1.0); climbing out of deep water costs an extra +1.0 Energy. An agent that runs out of energy in deep water is trapped. Respawns still avoid deep water. Random fords (deep water → shallow water conversion) appear as natural crossing points.

**Eating toggle**: The Eat action is not a one-shot operation but toggles an eating state. While in the eating state, energy is automatically extracted from food each tick; performing any non-Eat action (move/attack/signal/drink) exits the state. An eating agent takes 110% damage when attacked (feeding vulnerability), forcing a trade-off between safety and energy gain.

**Food competition**: When multiple agents eat the same plant simultaneously, per-tick extraction efficiency decays as 1/√n (e.g., 2 agents each get 71%, 3 each get 58%), creating scramble competition pressure.

**Corpse pollution & dynamic decay**: Agents within 2 cells of a corpse suffer extra HP decay (max 0.06/tick on the corpse cell), preventing agents from clustering around corpses and encouraging exploration. Corpse decay rate is driven by temperature (heat accelerates, cold slows) and humidity (wet season accelerates). Large corpses (> 50 Energy) release 2× nutrients, enriching the soil and forming natural oasis effects.

**Attack cooldown**: After each attack, the agent cannot attack again for 4 ticks, preventing continuous burst kills. Attack damage scales with Energy ratio (lower Energy = lower damage, higher Energy = higher damage), encouraging health management over mindless aggression.

**Progressive aging**: After 5,000 ticks, agents begin to age (extra decay +0.02/tick), accelerating to +0.05 at 6,000 ticks, reaching +0.1 at 7,000 ticks, with natural death at 8,000 ticks. This ensures genuine generational turnover and prevents super-individuals from monopolizing the population.

**Energy-First**: Agents have Energy (0–100) as the unified currency. All active actions (move/attack/signal) deduct directly from Energy. Energy has no passive recovery — only eating provides energy; the food chain is the sole energy source. Attack cost = AttackCostBase / BodyStrength (stronger = more efficient). After 5 consecutive ticks without moving, the agent enters stationary status — takes 1.2× damage when attacked (vulnerability), but generates extra heat on own and neighboring cells (+0.5°C / +0.3°C). When Energy drops below LowEnergyThreshold (10), movement has a 50% failure chance.

**Minimal reward**: Only survival and eating are rewarded. No reward for attacking, cooperating, or communicating. Whether attacking is valuable, cooperation is beneficial, or signals are useful — the agents must discover this entirely on their own.

**Heritable body parameters (Genome)**: Agent body attributes (Size / Speed / Strength / VisionRange / FatStorage / ColdResistance / HeatResistance) are encoded by the Genome and passed to offspring via Gaussian mutation (std = 0.05) during reproduction. Each parameter is coupled with survival through the energy economy: larger Size → higher base metabolism, higher Speed → cheaper movement, higher Strength → greater attack damage & lower attack energy cost, higher FatStorage → stronger pheromone emission, higher ColdResistance → less cold damage, higher HeatResistance → slower high-temperature water loss. This provides the evolutionary foundation for niche differentiation — agents cannot be "good at everything"; they must specialize.

The core hypothesis of this design is: **If a behavior cannot emerge in a minimal system, it's unlikely to be "truly" emergent in a larger, more complex system — it's more likely the programmer's bias at work.**

---

## Technology & Principles

The foundation is PPO (Proximal Policy Optimization) + GAE (Generalized Advantage Estimation). An agent's brain is a CNN+GRU hybrid network: the CNN processes the 7×7×6 directional grid to extract spatial features; the GRU fuses spatial features with non-grid state for temporal decision-making. All agents share a single BatchBrain (GPU batched inference); PPO trains continuously online. After each PPO update, GRU hidden states are cleared to ensure the policy update does not carry stale temporal bias from the old policy.

The world **never resets** — it uses an individual reincarnation mechanism: each agent, after dying, waits ~5 seconds (25 ticks) and then respawns in a new location, inheriting the shared PPO policy's experience (GRU hidden state cleared, but network weights continue evolving). Every 16 respawns counts as one Generation; every 8 generations, the best weights are saved to the database.

Genetic algorithms are no longer used for wholesale population replacement but for reproduction: agents with long survival and high HP can produce offspring via asexual reproduction (with weight mutation); offspring join the population (subject to the population cap).

The tech stack is simple: .NET + TorchSharp for inference and training, React + Canvas for visualization, WebSocket for real-time state push.

**GPU-first memory architecture** (v4.3): PPO experience buffers reside directly in GPU memory (pre-allocated tensors), incrementally written each step rather than via bulk PCIe transfers. StateMatrix reuses a GPU tensor to avoid per-tick allocation/deallocation. Per-tick temporary arrays are pooled and reused to reduce GC pressure. All inference and training happens on the GPU; world simulation stays on the CPU.

---

## Observation & Debugging

The real-time dashboard provides multiple observation dimensions:

**Top bar** — Displays world clock (☀/🌙 icon), temperature (color-coded: white < 5°C → blue → green → orange → red > 35°C), generation, deaths, alive count, online status. The stats area shows average lifespan, night death rate, and per-capita attack/eat/signal counts.

**Event monitor** — Structured world event stream: eat, attack, death, reproduce, respawn, chemical signal emission, natural disaster. Filterable by event type, with support for clearing and auto-scrolling.

**3D View** — A "3D" toggle button switches from the 2D Canvas to a Three.js 3D rendered view. The 3D view supports mouse rotation/zoom/pan (left-drag rotate, middle-drag zoom, right-drag pan). Terrain is rendered with height displacement (HeightMap 0–255 → 5 units height difference), 8-color biome vertex coloring, agent/plant/corpse InstancedMesh rendering (single draw call), a semi-transparent blue water surface, and lighting that follows the day/night cycle (azimuth + color temperature + intensity variation).

**Display toggles** — Multiple visualization layers can be overlaid (default all on: Scent-ZHI, Scent-Food, Direction, Vision):

- **Scent-ZHI** (purple) — Scent traces left by moving agents, decaying and diffusing each tick. Shows agent activity hotspots.
- **Scent-Food** (green) — Scent emitted by plants and corpses, independent of agent scent diffusion. Plant scent intensity is proportional to energy, helping agents locate high-energy food.
- **Direction arrows** — Agent facing direction, determining the vision cone.
- **Vision area** — The 7×7 cone-shaped visible area based on agent facing (shrinks to 3×3 circular when stationary).
- **Chemical field** (yellow) — Persistent chemical pheromone traces in space (continuous values), radius-4 circular diffusion + 4-neighbor diffusion, bright center fading to edges, naturally decaying. Shows communication hotspots.
- **Biome** (8 colors, default off) — Water (blue), River Bank (light green), Desert (yellow), Grassland (green), Jungle (dark green), Wetland (cyan), Highland (brown), Valley (olive).
- **Flow** (cyan arrows, default off) — 8-direction arrows showing river flow direction; visible only when zoomed in sufficiently.
- **Temperature heatmap** (blue → orange, default off) — Per-cell exact temperature values; visually observe heat-island effects and river cold-belts.
- **Surface water** (blue, default off) — Dynamic surface water depth (rain + river); opacity increases with depth.
- **Groundwater** (indigo, default off) — Groundwater saturation 0–1; plants consume groundwater to grow.
- **Nutrient** (brown → green, default off) — Soil nutrient concentration (0–10), sourced from corpse and plant decay, consumed by plants.

**Niche classification** — The system automatically classifies each agent's ecological niche based on dietary history: 🌿 Herbivore (food > 70%) / 🥩 Carnivore (attack > 50%) / 🦴 Scavenger (corpse > 50%) / 🍽 Omnivore (balanced). Agent cards display a colored niche label (green / red / yellow / gray), recomputed every 200 ticks. Niches emerge from bottom-up rules — agents are not assigned roles; they differentiate naturally through dietary habits.

**Tracking system** — Right-click an agent on the map to pin it at the top; double-click to toggle tracking. Agent cards and map tooltips show the current reincarnation count (Gen N) and biological parent ID (← #parentId). With "Rebirth Track" enabled, when a tracked agent dies, the system automatically waits for its respawn (same slot) and continues tracking the next life. **Lineage tracking**: SQLite records all birth events (parent_id → child_id, including Genome snapshots of both), tracing ancestor chains up to 5 generations deep.

**Attack damage** — Floating text system: attack damage shows red "-N", eating recovery shows green "+N", death shows gray "DEAD", respawn shows purple "RESPAWN". Agents standing on food take 110% damage when attacked (feeding vulnerability); agents can choose to abandon food and flee.

**World configuration** — When creating a world, a modal allows full configuration of simulation parameters (grid size, food, temperature, combat, rivers, physiology), presented in collapsible sections. The Dashboard info area shows real-time attack rate (ATK), average lifespan, night death rate, and other statistical indicators.

**Day/Night & Temperature** — The header displays current world time (☀ HH:MM) and temperature (°C). Temperature affects agent survival pressure — nights are more dangerous; huddling is safer. The map can overlay a temperature heatmap, blue (< 5°C) → green (15–25°C) → orange (25–35°C) → red (> 35°C), at 25% opacity.

---

## Nine Stages of Evolution

This experiment has a natural exploration path:

**Stage 1: Stable foraging.** Can agents learn not to starve? This is the most basic survival capability.

**Stage 2: Scent navigation.** Agents leave scent trails (purple) when moving; plants emit food scent (green, intensity proportional to energy). Can agents learn to use both scent gradients to track resources?

**Stage 3: Signal utilization.** Broadcasting signals has a cost (3 Energy) but can convey information. Will agents develop semantics for signals?

**Stage 4: Predation & scavenging.** Attacks directly deduct Energy; corpses are edible resources. Will agents differentiate into "hunters" and "scavengers"?

**Stage 5: Strategy differentiation.** Will stable survival strategy types emerge — explorers, settlers, predators, scavengers, opportunists?

**Stage 6: Niche differentiation.** Plant distribution is driven by water and nutrients. Will agents develop strategy differentiations like "forage near water" or "settle in high-nutrient zones"?

**Stage 7: Water-source exploration.** Can agents learn to follow river sounds to find water? Will territories form around water sources?

**Stage 8: Shared semantics.** If signals are genuinely being used, will different agents' interpretations of the same signal converge?

**Stage 9: Diurnal adaptation & huddling.** Cold nights increase survival pressure. Will agents learn to actively approach others to huddle for warmth at night? Will diurnal activity rhythms emerge — forage by day, cluster by night?

---

## On Reincarnation

Agents die. But death is not the end.

Each agent, ~5 seconds after death, respawns in the same slot — carrying the latest shared policy (network weights) but with short-term memory cleared (GRU hidden state). This is a digital version of reincarnation: individual experience perishes with death, but strategic wisdom accumulates across the population.

Every 16 respawns counts as one "Generation"; every 8 generations, the best policy weights are saved. The world never stops; evolution continues through the endless cycle of birth and death.

We observe the trajectory of strategy evolution. Generation after generation — does foraging efficiency improve? Does attack frequency change? Does signal usage rise? These macro trends are more meaningful than any single agent's behavior.

---

## Core Hypothesis

> Complex behavior does not need to be designed.
>
> When individuals are given survival pressure, finite resources, local perception, and action capability — cooperation, competition, communication, and even social structures can all naturally emerge from the ecosystem as survival strategies.

If this hypothesis holds, ZHI will demonstrate a new path toward artificial intelligence — not through larger models, more data, or more sophisticated alignment techniques, but through building a sufficiently rich environment and letting intelligence grow on its own.

If the hypothesis fails, ZHI is equally valuable — it will show us where the boundaries of emergence lie, which behaviors are "natural," and which require a programmer's guidance.

Either outcome is an answer.

---

*ZHI is named after the Chinese character "栀" (zhī), the gardenia flower. It blooms in silence, asking nothing of who it blooms for.*
