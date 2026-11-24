# P2 隐藏信息信念与世界采样

## 信息边界

| 级别 | 内容 | 在 P2 中的处理 |
| --- | --- | --- |
| 公开 | 职业、当前回合、英雄和战场、法力、手牌/牌库/奥秘数量、公开的随从死亡与行动 | 用显式字段和结构化历史捕获 |
| 己方私有 | 己方手牌实例及修正、自己布置的奥秘、己方待决策选项、明确已见的牌库顶 | 仅指定席位可用。牌库顶由调用方传入已观察证据 |
| 对手隐藏 | 手牌身份与顺序、实例 ID、费用/攻击修正、生成标记、奥秘身份、牌库身份和顺序 | 采样器只接收数量和历史。模拟实例从样本重新分配 |
| 权威内部 | RNG 进度、实例编号计数器、未公开墓地、追踪术缓存、延迟结算队列和对手决策帧 | 假想世界用独立种子和编号。墓地只保留公开随从死亡并重编号。公开延迟回手由事件重建。对手待决策时拒绝构建 |

PublicHistory 在规则结算处记录事件，包含席位、类型、顺序、可见范围及公开载荷。记录了出牌、弃牌、爆牌、牌库移除、普通抽牌、换牌、回手、生成、已知洗入、奥秘布置和揭示，以及可以证明的未触发检查。抽牌匿名，奥秘布置匿名，换牌数量只对本人可见。生成牌的公共事件只发布公开或可由已知规则确定的身份，私有生成身份只对接收方可见。过滤后重编号，避免通过事件编号空缺推断私有事件。局域网协议及网络快照未增加历史字段。

DeterminizationBuilder 从显式白名单直接构建脱敏基底。它跳过两副牌库内容、对手手牌实例及对手奥秘身份，省略未公开墓地、隐藏生成标记和双方真实随机数进度。BeliefSampler 的参数只包含脱敏信息与先验。Build 只接受从同一个 PlayerInformation 得到的样本。

## 信念和概率

- 已知模式验证双方授权的完整初始牌组。逐事件对账得到剩余原始牌、已知回手或生成牌、必须留在牌库的洗入牌和当前奥秘槽。遇到同名原始与生成牌可解释同一动作时保留来源分支，无法对账返回诊断。未知手牌和未知牌库按剩余卡牌的可交换先验划分，再随机排序。调用方明确给出的己方已见牌库顶保持顺序。
- 未知模式只要求己方初始牌组。默认可替换的 IDeckPrior 支持带权原型和通用构筑先验。按实际卡池、职业、张数、重复上限、传说限制、已实现效果及公开消耗下界构造，候选再次经过 DeckRules.Validate 和同一历史对账。默认重试上限 64，避免在信息不足时无界重试。
- 奥秘槽按布置顺序保存。抽样使用可行有序分配的剩余卡牌重数，并排除同时存在的同名奥秘。只有在整段检查没有其他奥秘介入、触发条件明确时加入负证据。测试覆盖法师反制、猎人攻击类奥秘、圣骑士受伤类奥秘。多奥秘干扰后无法判明历史路径时返回诊断。
- 来源分支的权重和未知原型权重属于模型先验。它们并不表示对手实际构筑或策略概率。当前对账模型不从对手的出牌策略推断留牌偏好。

## P1.5 与下一阶段调用

```csharp
var builder = DeterminizationBuilder.Capture(liveGame, viewerSeat);
var belief = DeckBelief.Create(builder.Information, authorizedOwnDeck,
    authorizedEnemyDeckOrNull, optionalDeckPrior);
var sample = new BeliefSampler().Sample(belief, seed);
if (!sample.Success) {
    // Record only the public diagnostic and sampling seed.
    return;
}
var world = builder.Build(sample);
var view = world.Observe(viewerSeat);
var decision = world.ObserveDecision(viewerSeat);
if (decision != null) {
    var branch = world.Fork();
    branch.SubmitDecision(viewerSeat, decision.Id, decision.Options[0].Id);
}
```

P3 可复用同一个 belief 生成多个独立世界。策略应仅收到按席位观察。决策拥有者的既有候选随脱敏基底保留，未来候选在分支结算时由规则引擎生成。当前不支持在对手私有待决策期间重建世界，也不支持对无历史的模拟子节点重新做信念对账。

## 支持边界

适用于正常构筑、已完成换牌、具有结构化历史的对局。对尚未建模的私有生成、指定抽牌、复杂复制或手牌变形、手牌成长/减费、多奥秘互扰，以及普通洗入牌经历未知抽取等情况，当前可能拒绝采样。竞技场、现开、局域网脱敏副本和仍在动画中等待规则命中的状态也不在支持范围。未知构筑使用条件先验和有限重试，对复杂历史不承诺精确后验概率。模拟器继续通过正式引擎执行这些规则，只是对应真实历史可能无法反推出可验证的根世界。

## 验证与复现

- P2 专项 6,338 项，含冰法、动物园、奇迹贼每套 1,000 个独立样本和世界构建，真实隐藏内容置换不变性，三个职业的奥秘负证据，独立 RNG，超几何频率、原型先验权重、奥秘重数统计及待决策 Fork。部分世界经正式规则推进至疲劳终局。
- 各套世界构建和执行检查出错时，测试写入 work/belief-failure.txt，内容为种子、假想样本和操作轨迹。
- 21 个规则专项共 31,466 项，含 P0、P1、P1.5、三个旧 AI 和相关规则。

```powershell
$env:TOUCHFISH_BELIEF_BENCHMARK = '1'
$env:TOUCHFISH_SIMULATION_BENCHMARK = '1'
.\scripts\test.ps1 -Scope belief-sampling,simulation,dynamic-decisions
Remove-Item Env:TOUCHFISH_BELIEF_BENCHMARK,Env:TOUCHFISH_SIMULATION_BENCHMARK
```

## 单线程基准

2026-11-12，Windows 10.0.26200，Core i7-13620H，64 位 .NET Framework 4.8.09221，优化编译。预热后计时，GC 是测量窗口内各代回收次数。信念构建含历史对账，已知模式后续采样复用对账结果。12 和 24 指观察历史事件数。世界构建复用同一合法样本，末行额外走两次真实换边和抽牌。

| 负载 | 样本 | µs/次 | 次/秒 | 分配 B/次 | GC Gen0/1/2 |
| --- | ---: | ---: | ---: | ---: | --- |
| 已知牌组信念构建 | 3,000 | 13.63 | 73,380 | 15,538 | 7/0/0 |
| 未知牌组信念构建 | 3,000 | 148.91 | 6,715 | 108,479 | 51/1/0 |
| 已知采样，12 事件 | 10,000 | 9.04 | 110,635 | 11,313 | 17/0/0 |
| 已知采样，24 事件 | 10,000 | 8.94 | 111,826 | 11,988 | 19/0/0 |
| 未知采样，12 事件 | 5,000 | 55.60 | 17,985 | 52,249 | 41/1/0 |
| 假想世界构建 | 10,000 | 1.72 | 582,588 | 4,461 | 7/0/0 |
| 构建并真实结算两步 | 5,000 | 8.44 | 118,467 | 22,371 | 17/0/0 |

按实测窗口折算，每 1,000 次已知采样分配 11.31–11.99 MB，Gen0 约 1.7–1.9 次。每 1,000 次未知采样分配 52.25 MB，Gen0 约 8.2 次，Gen1 约 0.2 次。GC 为窗口平均，单次批次有波动。

| P1 / P1.5 原基准 | P1.5 | P2 复测 |
| --- | ---: | ---: |
| 捕获正式对局快照 | 6,094 次/秒 | 6,272 次/秒 |
| 普通 Fork | 692,148 次/秒 | 695,941 次/秒 |
| Fork 加三步 | 3,547 次/秒 | 3,585 次/秒 |
| 完整对局 | 144 局/秒 | 147 局/秒 |
| 待决策 Fork | 0.97 µs | 0.97 µs |
| 生成三个候选 | 2.23 µs | 2.08 µs |
| 提交并结算 | 10.45 µs | 10.07 µs |

## 文件

- 新增 src/PublicHistory.cs、src/BeliefSampling.cs、src/DeterminizationBuilder.cs、tests/BeliefSamplingChecks.cs。
- 历史接入涉及 src/MatchEngine.cs、src/SecretRules.cs、src/DiscardRules.cs、src/NaxxRules.cs、src/RogueRules.cs、src/ConditionalTriggerRules.cs、src/MageRules.cs、src/DruidSpellRules.cs、src/GvgRules.cs、src/GvgClassRules.cs、src/HunterRules.cs、src/LowCostRules.cs、src/WarlockRules.cs、src/PriestRules.cs、src/MidCostRules.cs、src/TurnEndRules.cs、src/ComboRules.cs、src/BlackrockRules.cs。
- 快照接入修改 src/SimulationSnapshot.cs。验证入口修改 tests/DynamicDecisionFixture.cs、tests/RuleTests.cs、tests/core-sources.json、scripts/test.ps1。
