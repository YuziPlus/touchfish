# P5.0 Extreme 自我对弈

从项目根目录执行，默认一局动物园对冰法。两个席位都由 ExtremeSearch 操作。

```powershell
.\scripts\self-play.ps1
.\scripts\self-play.ps1 -Config tools/SelfPlay/functional.json -Output work/p5-functional
.\scripts\self-play.ps1 -Config tools/SelfPlay/benchmark.json -Output work/p5-benchmark
.\scripts\self-play.ps1 -Verify
python tools/SelfPlay/read_dataset.py work/p5-functional
```

入口以 Windows .NET Framework 编译纯规则源码和独立运行器，EXE 放在 work/self-play-bin。数据目录可以单独移动。每个席位拥有本局独立的 ExtremeSearch 实例与 RNG。采用 P3 默认冷启动，复用 P1 模拟、P1.5 动态决策、P2 信念和现有 Extreme 换牌。两个换牌决定都在第一方确认前生成。规则引擎执行并验证全部动作，离线固定回合钟避免机器速度影响结果。

## 配置与续跑

- Games 是总局数，Seed 是调度种子，Iterations 是每个常规根决策的上限，MulliganSamples 是每方的换牌采样上限。MaxDecisions 包含换牌。
- Decks 指定两个注册 ID；设为 null 则每对从 Pool 有放回抽取。CustomDecks 可为自定义 ID 指定 Deckstring，PoolId 必须匹配。内置 ID 为 zoo、freeze、miracle。
- FirstSeat 支持 paired、random、seat0、seat1。paired 为相邻两局使用相同构筑和 game_seed，仅交换先手。奇数局最后一局没有搭档。
- PriorPool 是单独声明的等权公开先验。P2 只能看到整个先验池及对手公开职业，并按自身约束采样。为空时使用 P2 的通用合法构筑先验。运行器不将选中的真实对手构筑传给搜索。manifest 的实际配对、配置中的牌池和种子用于复现与划分，不能作为模型观察特征。
- Parallelism 限定并行对局数。Compression 选择 GZip 或原始 JSONL。Diagnostics 选择成功对局的引擎日志，异常总有独立 error.json。诊断和元数据不属于训练输入。

以同一输出目录和配置重跑即可续跑。完成一局后先原子写入数据，再提交包含 SHA-256 的收据。收据是续跑的提交点。未写收据的孤立分片会在重跑时替换。进程硬退出后重新运行会恢复 manifest。运行目录锁防止两个进程同时发布。源码或牌库内容变化会改变构建指纹，需改用新目录。Parallelism 和 Diagnostics 可在续跑时调整。熔断、取消、执行失败不提交训练样本。正常终局才赋予 z。

## Schema 1

每个分片包含一局，UTF-8 JSONL，可选 GZip。manifest.json 列出正式分片、SHA-256、字节数、样本数、对局 seed、席位构筑和终局。summary.json 记录进度、搜索诊断、耗时与 GC。config.json 固化影响对局或数据的配置。game_id 在一个运行内唯一，多运行合并时应另加运行命名空间。

| 字段 | 含义 |
|---|---|
| schema_version, game_id, game_seed, decision_index, seat, turn, player_class | 格式、复现与分组元数据 |
| model, search_config, mulligan_samples | 策略来源与预算，当前为 extreme-heuristic-baseline |
| observation | 行动前的 P0 观察，包括公开英雄、法力、棋盘和数量，己方手牌、己方已知奥秘、初始构筑清单、可见历史及本人动态候选。历史是从开局至此的可见事件前缀 |
| legal_actions | 变长候选集合。kind 区分 play、attack、power、end、decision、mulligan。source_zone、source_slot、source_card 定位本方实例，目标由 target_side、target_zone、target_slot、target_card 描述。position 是召唤插入位置，choice 是抉择或动态选项，option_index 对应当前动态候选，replace_slots 是换牌子集。索引从 0 开始 |
| selected_action | 对应本行唯一候选 key，key 按相对槽位、牌 ID、目标和选项构造 |
| visits, value | 候选上的原始根访问和均值。常规搜索估值使用 P3 的 0–1 尺度。换牌 value 是同尺度的候选均值，没有 UCT visits。无统计用 null。强制动作不补访次 |
| search | extreme、mulligan 或 forced；迭代、成功世界数、采样尝试与失败、停止与回退原因。换牌迭代为 0，按成功世界数单独衡量 |
| z, final | 当前席位终局标签 +1/-1/0，以及双方最终生命、赢家、对局长度。仅用作标签和诊断 |

正式对局观测保留已有公开和己方可见实例 ID 以便诊断。模型应使用相对位置与卡牌 ID，舍弃运行时实例编号、game_seed、终局字段及日志。观察没有真实敌方手牌、牌序或未揭示奥秘，己方初始构筑按牌 ID 排序也不含牌序。候选特征可拼接观察对应槽位的实体向量。动态选项的结构对应当前 Observation.Decision.Options。对同一手牌实例的不同目标或站位保留单独候选。

## P5.1 读取接口

`read_dataset.iter_games(directory)` 以整局为单位校验并读取。`examples(directory)` 逐样本提供 `split, observation, candidates, selected_index, policy_target, z`。Policy 可用有效根访问归一化，仅对有访问数据的行构造目标。Value 可直接读取 z，代表该轨迹中行动方最终结果。划分按 seed 哈希，同局和先手交换局进入同一侧。校验包含收据哈希、显式观察字段、私有事件受众、合法候选引用、强制动作统计和标签视角。Python 校验不会重跑炉石规则，规则合法性由生产时的引擎执行保证。

## 验证

功能组为固定种子 512021 的 10 局，覆盖 zoo/miracle、freeze/freeze、miracle/freeze、zoo/freeze 和先手交换。另跑 zoo/zoo 镜像。定向验证通过 P1.5 隔离测试牌覆盖连续动态选择和随机选项，使用正式经典牌覆盖奥秘触发。这三套固定构筑的实跑样本没有动态选择行。

将 10 局中的 3 个完成分片和收据复制到新目录，模拟进程只提交部分局数便退出。三并行续跑补齐 7 局，10 个分片 SHA-256 与单线程逐一匹配。再次运行新增 0 局。结果表见生成目录。

## 20 局实测

使用 benchmark.json，单线程、每个搜索根最多 16 次迭代、每方换牌采样 4 个世界、压缩开启。

| 指标 | 结果 |
|---|---:|
| 完整终局 | 20 / 20 |
| 批次墙钟 | 35.75 s |
| 每局 / 吞吐 | 1.787 s / 0.559 局每秒 |
| 决策与样本 | 1606 条，均值 80.3 条每局 |
| 压缩训练数据 | 549005 B，均值 27450 B 每局 |
| 采样失败 | 0 / 20736 |
| 回退 | 0 / 1326 次搜索或换牌 |
| 强制动作 | 280 |
| 峰值工作集 / 结束时托管内存 | 53.3 MiB / 6.73 MiB |
| GC Gen0 / Gen1 / Gen2 | 3534 / 246 / 43 |

同一机器、相同预算与构筑分布、单线程线性估算，仅计压缩训练分片，元数据和文件系统开销另计。

| 局数 | 时间 | 数据 |
|---|---:|---:|
| 1,000 | 0.5 小时 | 27.5 MB |
| 10,000 | 4.96 小时 | 274.5 MB |
| 100,000 | 49.6 小时 | 2.75 GB |

20 局批次复跑的分片 SHA-256 逐一一致。峰值工作集和 GC 仅覆盖 20 局窗口。决策上限熔断测试留下 error.json，正式数据为零局。原始 JSONL 动物园镜像也通过读取校验。
