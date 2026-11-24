# P1.5 动态决策交付与基准

## 结算与接口

1. 出牌走正式引擎的校验、扣费、离手、出牌和施法触发、奥秘反制、入场及战吼前置处理。
2. 动态效果登记为不可变程序。引擎按当前步骤生成带稳定 ID 的选项，保存决策席位、候选、下一步骤、来源卡、随从和出牌监听者。召唤通知暂存于可复制的 FIFO。
3. 只向决策席位返回独立的观察数据。校验席位、决策 ID 和选项 ID 之后，才应用选中的效果。下一步骤可再生成一轮决策。全部完成后，继续原有的召唤通知、过载、清理、施法完成、随从奥秘及出牌完成流程。对局已经结束时终止动态结算。
4. 快照深复制待决策对象、选项数组、执行帧和召唤通知队列。监听者和随从引用重新绑定到分支自己的实体。已经生成的候选保留，后续候选继续使用分支 RNG。未指定种子时接着原 RNG 进度运行，指定种子只影响未来抽样。

| 接口 | 用途 |
| --- | --- |
| MatchEngine.HasPendingDecision / FastSimulation.HasPendingDecision | 判断是否等待选择 |
| MatchEngine.ObserveDecision(seat) / FastSimulation.ObserveDecision(seat) | 获取指定席位有权查看的决策 ID、提示和候选 |
| MatchEngine.SubmitDecision(seat, decisionId, optionId) / FastSimulation.SubmitDecision(...) | 提交并继续真实结算 |
| AgentObservation.Decision / AgentBridge.SubmitDecision(...) | AI 的按席位观察和提交 |
| IDecisionMatchAgent.ChooseDecision(...) | 可选的 AI 决策接口，沿用 IMatchAgent；旧 AI 在本地遇到动态决策时选第一项 |

动态选项使用稳定键。测试发现从隔离候选池按配置权重、不放回抽取三项。选中牌走既有生成牌流程，满手牌记录为烧牌。连续发现各自生成决策 ID，费用和战吼只处理一次。当前钟到零会等待已开始的选择完成，之后按原有自动换边逻辑处理。

## 文件

- 规则与状态新增 src/DynamicDecisions.cs。修改 src/MatchRules.cs、src/MatchEngine.cs、src/LowCostRules.cs、src/TurnClockRules.cs、src/SimulationSnapshot.cs、src/FastSimulation.cs。
- AI 与界面新增 src/DynamicDecisionUi.cs。修改 src/MatchAgent.cs、src/MatchAgentUi.cs、src/MatchUi.cs、src/LanUi.cs。
- 验证新增 tests/DynamicDecisionFixture.cs、tests/DynamicDecisionChecks.cs、tests/Wpf/DynamicDecisionVerification.cs。修改 tests/SimulationChecks.cs、tests/RuleTests.cs、tests/core-sources.json、tests/Wpf/VerificationRegistry.cs、scripts/test.ps1、build.ps1。

## 验证与边界

- 15 个规则专项共通过 23,986 项。动态决策专项 13,031 项，包含 1,000 个初始待决策状态，每个状态的三个候选均分别走完两轮结算。P0、P1、冰法、动物园、奇迹贼、追踪术、德鲁伊抉择和相关规则均通过。
- WPF 通过 7 个专项，其中动态决策有 13 项。普通和极简模式都通过按钮路由事件完成两轮决策。正式客户端编译和 WPF 外观预览通过。
- 当前可暂停点位于卡牌效果阶段，决策方为出牌者。支持逐步选择并生成牌或对敌方英雄造成伤害。历史抉择和追踪术继续走出牌前选择。亡语、奥秘及任意运行中回调内部的新暂停点、联机决策传输、正式发现卡和历史职业权重留待后续。测试卡只编入验证程序。
- P2 可在可信层用采样后的权威状态创建快照。遇到已生成的决策时保留现有候选，以相同种子分别 Fork 后提交各自的选项 ID。后续选项由分支引擎生成。策略层只收对应席位观察，禁止把权威快照交给策略或局域网客户端。

## 单线程性能

2026-11-12，同一台 Windows 10.0.26200、Core i7-13620H、64 位 .NET Framework 4.8.09221，优化编译。P1 值取自此前同机报告，P1.5 为本轮测量。耗时和吞吐量随系统调度波动。

| P1 相同负载 | 样本数 | P1 吞吐量/秒 | P1.5 吞吐量/秒 | P1.5 耗时 | 分配 B/次，P1 → P1.5 | GC Gen0/1/2，P1 → P1.5 |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| 捕获正式对局快照 | 300 | 6,325 | 6,094 | 164.09 µs | 404,138 → 404,097 | 19/7/0 → 19/7/0 |
| 已有快照 Fork | 30,000 | 679,206 | 692,148 | 1.44 µs | 7,029 → 6,965 | 33/0/0 → 33/0/0 |
| Fork 后结算三步 | 12,000 | 3,414 | 3,547 | 281.95 µs | 91,533 → 91,469 | 174/4/0 → 174/4/0 |
| 完整模拟对局 | 160 | 136 | 144 | 6.95 ms | 3,254,524 → 3,254,364 | 82/2/0 → 82/2/0 |

| 新负载 | 样本数 | 吞吐量/秒 | 平均耗时 | 分配 B/次 | GC Gen0/1/2 |
| --- | ---: | ---: | ---: | ---: | --- |
| 同场面对照 Fork | 30,000 | 1,601,854 | 0.62 µs | 3,715 | 17/0/0 |
| 待决策 Fork | 30,000 | 1,028,200 | 0.97 µs | 4,582 | 21/0/0 |
| 生成三个候选 | 50,000 | 448,217 | 2.23 µs | 2,889 | 22/0/0 |
| 提交并完成真实结算 | 6,000 | 95,736 | 10.45 µs | 8,161 | 7/1/0 |

同场面对照均有两名随从、空手牌及相同牌库，待决策样本另保存决策和一个召唤通知。提交测量提前构造所有分支，只计提交、生成牌、秃鹫抽牌及规则尾部。分配为累计分配字节差，GC 为测量窗口内各代次数。普通路径未见明显回退。待决策 Fork 比同场面对照多约 0.35 µs 和 867 B。

```powershell
$env:TOUCHFISH_SIMULATION_BENCHMARK = '1'
.\scripts\test.ps1 -Scope simulation,dynamic-decisions
Remove-Item Env:TOUCHFISH_SIMULATION_BENCHMARK
.\scripts\test.ps1 -Scope dynamic-decisions,ai,zoo-ai,miracle-ai,hunter,druid-spells,pyroblast -Layer ui
```
