# 黑石山的火焰：v1.0.0实现

本批实现全部31张可组牌卡，及小鬼、两种烈焰德鲁伊形态、黑色雏龙、拉格纳罗斯英雄与技能共6条辅助记录。当前566张可组牌卡全部支持。正式版本号1.0.0。

## 数据与规则边界

- 固定数据源：https://api.hearthstonejson.com/v1/25770/zhCN/cards.json 。导入器校验原始文件 SHA-256，来源与选中原始记录分别保存在 data/sources/blackrock-provenance.json 和 blackrock-source.json。
- 采用原始2015卡牌数值：索瑞森6费5/5、黑铁潜藏者5费、掷斧者2/5、黑翼腐蚀者战吼3伤害。没有将2015全局平衡补丁倒灌进现有经典/纳克/GVG冻结包。
- GVG池归档，新池作为原卡池超集。旧保存卡组只在运行时转换，不重写用户文件。竞技与现开走同一当前池。保持职业、同名上限和辅助牌不可组牌规则。

## 共享机制

- BlackrockRules显式注册白名单，未知效果仍不可用。受伤召唤在来源右侧入场，遵守七槽、沉默和真实伤害；奴隶主须存活，小鬼首领、龙蛋和掷斧者允许致死受伤触发。
- 真实死亡批次维护DeathsThisTurn，换边清零；四张死亡减费牌共用该计数，LAN显式同步，不发送墓地私有历史。
- 龙王配偶减费跨回合保留，消费于下一张成功打出的龙牌；配偶自身是龙，连续打出会先消费旧减费再授予新的两点。
- 索瑞森减费绑定当前手牌实例；回手/重新生成不继承此前实例减费。
- 夜幕奇袭洗入基础复制（不复制战场增益），可选择双方随从及辅助小炸弹。遵守2015的60张牌库上限；私有生成来源允许抽到后使用跨职业随从，不放开混职业组牌。
- 克洛玛古斯只复制真实入手的抽牌，复制不是再次抽牌。通用抽牌、追踪、感知恶魔、海盗检索复用通道；生成、疲劳与原牌爆掉不触发。满手遵守十张上限。
- 龙人巫师在指定法术结算前成长，即使反制也成长；火妖在成功法术结算后响应，反制不触发，采用施法前监听者快照。
- 复活术从当前控制者的真正随从死亡记录选基础卡，不继承增益，不重复战吼。管理者走公共英雄替换定义与亡语系统，技能、8血上限和身份明确同步。

参考边界：[龙人巫师触发说明](https://hearthstone.wiki.gg/wiki/Dragonkin_Sorcerer)、[历史夜幕奇袭60张上限讨论](https://www.hearthpwn.com/cards/14462-gang-up)。

## 专项验证

- scripts/test.ps1 -Scope blackrock -Layer rules：109项，涵盖全部31牌及核心组合边界。
- 已通过受影响范围：hunter、warlock、low-cost、mage、triggers、overload、heroes、combo、resolution、privacy、library；以及GVG三批/中立、arena、sealed规则。
- WPF blackrock专项41项、TCP专项22项。覆盖组牌版本筛选、详情、搜索、抉择、条件战吼、普通与极简固定窗口；TCP专项覆盖双方席位出牌、夜幕洗牌数量、减费、死亡计数、英雄替换和技能。仅本机回环，不声称双电脑实测。
- build.ps1后运行正式Touchfish.exe --preview previews，并检查新生成WPF图片。未全量重跑旧模块。

## 卡牌清单

| ID | 名称 | 职业 | 费用 |
|---|---|---|---:|
| BRM_001 | 严正警戒 | PALADIN | 5 |
| BRM_002 | 火妖 | MAGE | 3 |
| BRM_003 | 龙息术 | MAGE | 5 |
| BRM_004 | 暮光雏龙 | PRIEST | 1 |
| BRM_005 | 恶魔之怒 | WARLOCK | 3 |
| BRM_006 | 小鬼首领 | WARLOCK | 3 |
| BRM_007 | 夜幕奇袭 | ROGUE | 2 |
| BRM_008 | 黑铁潜藏者 | ROGUE | 5 |
| BRM_009 | 火山邪木 | DRUID | 9 |
| BRM_010 | 烈焰德鲁伊 | DRUID | 3 |
| BRM_011 | 熔岩震击 | SHAMAN | 2 |
| BRM_012 | 火焰驱逐者 | SHAMAN | 4 |
| BRM_013 | 快速射击 | HUNTER | 2 |
| BRM_014 | 熔火怒犬 | HUNTER | 4 |
| BRM_015 | 复仇打击 | WARRIOR | 2 |
| BRM_016 | 掷斧者 | WARRIOR | 4 |
| BRM_017 | 复活术 | PRIEST | 2 |
| BRM_018 | 龙王配偶 | PALADIN | 5 |
| BRM_019 | 恐怖的奴隶主 | NEUTRAL | 5 |
| BRM_020 | 龙人巫师 | NEUTRAL | 4 |
| BRM_022 | 龙蛋 | NEUTRAL | 1 |
| BRM_024 | 龙人打击者 | NEUTRAL | 6 |
| BRM_025 | 火山幼龙 | NEUTRAL | 6 |
| BRM_026 | 饥饿的巨龙 | NEUTRAL | 4 |
| BRM_027 | 管理者埃克索图斯 | NEUTRAL | 9 |
| BRM_028 | 索瑞森大帝 | NEUTRAL | 6 |
| BRM_029 | 雷德·黑手 | NEUTRAL | 7 |
| BRM_030 | 奈法利安 | NEUTRAL | 9 |
| BRM_031 | 克洛玛古斯 | NEUTRAL | 8 |
| BRM_033 | 黑翼技师 | NEUTRAL | 3 |
| BRM_034 | 黑翼腐蚀者 | NEUTRAL | 5 |
