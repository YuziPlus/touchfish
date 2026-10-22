using System;
using System.Windows;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;

namespace Touchfish {
public static class AppRelease {
 public const string Version="v0.8.5",Date="2026-10-22";
 public static readonly string[] PatchNotes=new[]{"修复驯兽师、狂野怒火的目标限制：仅能指定己方野兽随从，拒绝非野兽、敌方随从和英雄。","无效目标不消耗手牌和法力，双方席位及联机权威采用相同校验。","驯兽师没有合法野兽时可无战吼上场；狂野怒火必须选择合法野兽。沉默保留种族，变形按当前种族判断。","保留0.8.4竞技场纳克卡池、选牌点击和奥秘隐私修复。"};
 public static readonly string[] Version084Notes=new[]{"补充修复竞技选牌文字区域点击无响应：避免内部滚动容器截获点击，按下一次只选择一张；保留滚动与键盘操作。","验证房主先选完、加入方先选完和交替选牌，双方均能继续选择并进入起手换牌。","竞技场启用经典＋纳克当前卡池，412张卡按所选职业过滤，纳克职业牌与中立牌均可被选出。","竞技选牌、卡组验证与联机握手使用同一当前卡池；鹰角弓采用纳克修订版本，辅助召唤牌不参与选牌。","保持普通70%、稀有20%、史诗8%、传说2%，同档三选一及第1、10、20、30轮稀有及以上；职业牌和法术/武器权重不变。","保留同名不限张数、30张开局和三局两胜流程。"};
 public static readonly string[] Version083Notes=new[]{"悬停己方奥秘问号可查看名称与效果；对手的悬停提示仅显示“奥秘数量:x”。","疯狂的科学家从牌库成功置入奥秘后，仅持有者收到约两秒的牌名提示，关闭特效或极简模式也可看到基础提示。","己方奥秘列表与私有提示按接收者发送；对手不接收未触发奥秘的名称，重复快照不重播提示。","修复奥金尼与肉用僵尸在法术内部同批死亡后的治疗转换：亡语按当前场面结算，法术自身快照保持。","修复救赎复活随从的召唤响应时机：先设一血再触发响应，后续伤害和公正之剑加成不再被覆盖。","旧经典保存卡组在标准对战、本地test和人机入口自动兼容当前纳克卡池；原保存代码和记录不自动改写。","联机明确区分牌库数据、程序构建和卡池不兼容；相同版本号也需要双方使用同一构建。","修复手牌变化后点击旧卡牌可能抛异常的问题：提示重新选择，保留合法攻击选择，不消耗资源。"};
 public static readonly string[] Version082Notes=new[]{"补齐剩余16张纳克萨玛斯中立牌，纳克30/30、当前卡池412/412全部可用；经典382张保持独立。","实现瑞文戴尔男爵亡语翻倍、克尔苏加德本回合死亡复活、双子召唤塔迪乌斯和回响软泥怪状态复制。","补齐送葬者、纳克萨玛斯之影、石像鬼、蛛网领主、哀嚎的灵魂及其余亡语；遵守沉默、控制权、同批死亡和七槽限制。","淤泥喷射者恢复2014年3/5数值，导入脚本同步修正；所有纳克召唤辅助牌支持回手和复制使用。"};
 public static readonly string[] Version081Notes=new[]{"纳克萨玛斯九职业卡，以及疯狂的科学家、洛欧塞布、死亡领主已可用。","补齐结网蛛随机野兽池所需的鬼灵爬行者与迈克斯纳；当前卡池支持396/412张。","普通模式特效低/高时显示爆牌牌面、碎裂动画和满手牌气泡；关闭时只在中央显示“牌名已破碎”，双方记录仍保留牌名。极简保持静默。","修正死亡之咬耐久度为2；奥秘从牌库置入不触发施法效果，不响应其进入战场前发生的死亡。"};
 public static readonly string[] Version080Notes=new[]{"现开开包与组牌行增加…详情按钮；加减卡牌与查看详情保留两个列表各自的滚动位置，对手进度刷新不再重建本方卡牌列表。","新增爆牌动画：手牌已满时抽到的卡牌在屏幕中央出现并碎裂；双方都能看到被爆卡牌的牌面。","爆牌时英雄显示‘我的手牌已经满了！’气泡；双方共享的对局记录显示被爆卡牌名称。","对手手牌数后显示其牌库剩余数量；牌库抽空后每次抽牌显示疲劳提示和递增伤害。","保持普通与极简窗口尺寸及手牌隐私；只公开爆牌卡牌，不公开双方手牌或牌库顺序。"};
 public static readonly string[] Notes={
  "首个正式版本，保留小尺寸、无卡图的办公面板外观。",
  "局域网对战：创建与加入房间，房主地址可一键复制，多网卡可切换；双方固定己方视角，操作自动同步，断线后停止操作。",
  "经典牌库：382 张可组牌，覆盖九职业与中立牌；预留独立卡池扩展接口。",
  "组牌与分享：按职业、费用和类型筛选，搜索名称与效果；卡组可保存，一键导出或导入代码。",
  "基础对战：攻击、法术、战吼、英雄技能、武器、法力与回合流程；先选来源再选目标，支持左右插入随从。",
  "共享记录：双方看到同一份公开对局历史，包含出牌、目标、攻击伤害、抽牌数量、疲劳、阵亡与回合切换。离开后可查看上局记录。",
  "手牌详情：随从显示费用、攻击和生命；武器显示攻击和耐久，完整效果可滚动查看。",
  "外观：WPS 与 Codex 风格，普通 / 极简模式；极简只显示攻、效果、血与详情入口。Codex 深色背景 #0D1117，文字 #E6EDF3。",
  "本地测试：仅从顶栏小 test 按钮进入自对战，正常对战入口为局域网。",
  "联机试运行：用户与朋友已实测，反馈可以运行。",
  "当前范围：211 / 382 张牌可对战，其余标注【暂不可用】。完整经典规则、回合倒计时与断线续局尚未完成。"
 };
}
public partial class PanelWindow {
 Grid releasePanel;Button releaseTab;
 void BuildReleaseNotes(){
  releasePanel=new Grid{Margin=new Thickness(12,8,12,8),Visibility=Visibility.Collapsed};releasePanel.RowDefinitions.Add(new RowDefinition{Height=new GridLength(24)});releasePanel.RowDefinitions.Add(new RowDefinition());Put(shell,releasePanel,4);var heading=new Grid();heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(64)});heading.Children.Add(T("更新日志 · "+AppRelease.Version,12,Ink));var updates=Btn("版本更新",64);updates.Height=22;updates.FontSize=10;updates.Click+=(s,e)=>OpenUpdates();Grid.SetColumn(updates,1);heading.Children.Add(updates);releasePanel.Children.Add(heading);
  var body=new StackPanel();
  body.Children.Add(ReleaseSection(AppRelease.Version,AppRelease.Date,AppRelease.PatchNotes));
  body.Children.Add(ReleaseSection("v0.8.4","2026-10-22",AppRelease.Version084Notes));
  body.Children.Add(ReleaseSection("v0.8.3","2026-10-20",AppRelease.Version083Notes));
  body.Children.Add(ReleaseSection("v0.8.2","2026-10-16",AppRelease.Version082Notes));
  body.Children.Add(ReleaseSection("v0.8.1","2026-10-16",AppRelease.Version081Notes));
  body.Children.Add(ReleaseSection("v0.8.0","2026-10-15",AppRelease.Version080Notes));
  body.Children.Add(ReleaseSection("v0.7.9","2026-10-15",new[]{"新增局域网现开赛：选职业后每人开18包，每包5张，共90张；从自己的牌池组30张，不受常规同名限制，但不能超过实际开出的张数。","三局两胜，局间可用原始90张重组；房主生成和校验，私有牌池不发给对手。当前仅发放已实现效果的本职与中立非基础牌。","按官方公开普通包的长期获得率、首次传说、各品质保底及防重复规则模拟；不沿用竞技权重，不声称复刻未公开内部抽样算法。金色仅为开包记录，不改变数值。","协议升级为4，保持普通与极简固定尺寸、办公外观，不写入个人卡组或偏好。"}));
  body.Children.Add(ReleaseSection("v0.7.8","2026-10-15",new[]{"竞技改为先抽稀有度档，再同档三选一；基础与普通合档，同轮三个选项不重复，跨轮允许重复。","普通轮概率为普通70%、稀有20%、史诗8%、传说2%；第1、10、20、30轮保证稀有及以上，按20:8:2抽取。","同档内职业牌权重乘1.5，法术与武器再乘1.5；集中配置选牌权重，为本项目规则，不声称官方精确概率。"}));
  body.Children.Add(ReleaseSection("v0.7.7","2026-10-14",new[]{"修复竞技选牌偶尔点击无效：对手进度刷新不再误解除本方尚未确认的选择请求，只在本方选项版本推进或收到拒绝回复后恢复点击。","忽略过期竞技选牌快照；进入对局后不再接收选牌状态，避免旧消息重新打开选牌界面。","修复竞技首局起手换牌被锁住：首个对局快照明确清除选牌等待状态并关闭选牌界面，不依赖命令回复编号。"}));
  body.Children.Add(ReleaseSection("v0.7.6","2026-10-14",new[]{"新增经典奇迹贼 AI：在人机挑战中选择，使用火车王、暗影步、加基森、隐藏与疾跑；随机先后手，正常法力开局。","奇迹贼联合规划解场、连击、范克里夫成长和加基森刷牌，按实际抽牌重新决策；只使用己方手牌、己方剩余牌库无序数量与公开场面。","加入不放回抽牌概率评估，结合当前法力、伺机待发与幸运币比较找牌收益；不读取对手私牌、牌库顺序或随机种子。","改善隐藏与随从的出牌顺序、冷血即时收益及公开飞刀风险评估；含敌方飞刀的新火车王路线不再认定为必然斩杀。","组牌页增加版本筛选入口：可查看全部已安装版本或指定版本；其他卡池仅供浏览，不改变当前卡组。当前仍仅内置经典旧世。","修复特效开启时加成攻击力未正确显示绿色的问题，保持办公界面与极简模式规则。"}));
  body.Children.Add(ReleaseSection("v0.7.5","2026-10-14",new[]{"新增短促攻击冲撞动画：随从和英雄武器攻击向实际目标冲撞后回位，全程约0.36秒；普通模式低/高特效显示，关闭或极简隐藏。伤害飘字和死亡碎裂的显示时机对齐碰撞；规则伤害仍即时结算，不增加双方操作等待。","攻击动画通过公开结构化事件同步与去重，记录攻击方和奥秘处理后的实际目标；非法攻击、被奥秘取消的攻击不播放。切换页面、极简或特效等级时清理动画并恢复原位置。","人机挑战从test移至对战页的模式选项，选择人机挑战后可选择经典冰法或经典动物园，使用自己的完整30张卡组开始；test仅保留十水晶本地双人自对战与取牌工具。","修复冻结解除时机：本回合新召唤且无冲锋的随从被冻结后，不会在当前回合末提前解冻；风怒随从或风怒英雄攻击一次后被冻结，可在错过本回合剩余攻击机会后解冻。普通攻击耗尽后被冻结仍持续至下一次己方回合结束。","统一公共目标结算接口：过滤离场、濒死、错误席位和重复候选；顺劈斩、多重射击、叉状闪电共用无放回多目标选择与批次伤害。教师与飞刀在施法阶段减少目标时，不再因缺少第二目标抛出异常；全体目标消失也安全结束。","同批伤害先固定目标，不追打亡语新召唤的随从；逐次飞弹仍逐发选目标。初始出牌条件不足仍在扣费前拒绝，合法支付后的目标变化不返还手牌或费用，过载及施法后触发照常结算。","公共增益与设生命入口统一拒绝濒死随从，防止在死亡处理前被后续加血救活；新增目标结算扩展约定及独立专项检查，保留同时伤害中受伤触发的原有语义。"}));
  body.Children.Add(ReleaseSection("v0.7.4","2026-10-13",new[]{"修复希尔瓦娜斯与憎恶的亡语连锁：憎恶已造成致命伤害的随从不再进入随机偷取候选，女王只选择仍存活的敌方随从，避免有活目标时偷取落空。","修复镜像实体与公正之剑：先完成随从属性复制，再结算接收方的召唤增益；复制随从正确保留 +1/+1，武器只消耗一次耐久。战歌指挥官的冲锋与己方光环也按复制后的属性结算。","修复零攻击血吼配合英勇打击：借助英雄临时攻击力攻击随从时，血吼攻击力最低保持为零，不再降成负数；随后使用升级！正确获得 +1 攻击和 +1 耐久。","修复紫罗兰教师与飞刀杂耍者导致法术目标提前死亡的异常：飞刀先杀死目标后，神圣之灵、心灵之火、死亡缠绕等定向效果安全失效，不再访问已离场随从。法术仍消耗费用、计入本回合出牌，正常结算过载与施法后触发。","修复食腐土狼在憎恶亡语连锁中被错误救活：已受到致命伤害的土狼不再获得后续野兽死亡的 +2/+1；仍存活的土狼继续正常获得增益。","修复沉默后的新增法术伤害：被沉默的随从随后受到大法师战吼赋予的法术伤害 +1 时，加成正常生效；再次沉默会清除新增法强，公开快照与客户端保持一致。"}));
  body.Children.Add(ReleaseSection("v0.7.3","2026-10-13",new[]{"特效设置改为关、低、高：高包含低，并额外开启死亡之翼与炎爆术大动画；极简始终隐藏。","炎爆术在命中时才结算伤害，任一方选择高特效时统一等待；全屏震动阈值改为单次伤害大于9。","竞技选牌右侧显示己方已选卡牌，按费用排序、独立滚动；修复竞技场起手换牌点击。","随从沉默显示×状态，低或高且普通模式时为红色，关或极简时为中性色。","补充光明之泉回合开始治疗验证，保留办公窗口、经典382张牌与联机隐私。"}));
  body.Children.Add(ReleaseSection("v0.7.1","2026-10-13",new[]{"新增竞技模式：局域网连接成功后，双方各从随机提供的三个职业中选择一个。","每人连续进行 30 轮三选一选牌，候选牌来自全部中立牌与所选职业牌；选牌内容仅发送给本人，双方共构筑 30 张竞技卡组。","竞技对战采用三局两胜，单局结束后自动开始下一局，任一方先赢两局即结束系列赛；保留普通对战规则、起手换牌与断线保护。","经典旧世冻结卡池的 382 / 382 张牌均可用。","顶栏 test 可选择经典冰法 AI 或经典动物园 AI，正常一费开局、随机先后手，自动换牌和操作；保留原双人测试工具。","人机仅读取己方手牌与公开场面，不读取对手私牌或牌库顺序；普通与极简保持原窗口尺寸。"}));
  body.Children.Add(ReleaseSection("v0.7.0","2026-10-13",new[]{"经典旧世全卡支持：冻结经典池 382 / 382 张可组牌卡均可用。","诺兹多姆在场且未沉默时，双方回合 15 秒，到期由房主自动换边；无有效来源时不限时，普通、极简和特效开关均不影响。","红龙将选定英雄生命设为 15，保留护甲、身份和上限，不当作伤害或治疗。","开局前可输入玩家名称，双方名称同步到普通英雄面板、提示与共享记录；极简仍只显示血量。","保留办公外观、固定窗口、局域网隐私、倒计时选择状态与按需测试。"}));
  body.Children.Add(ReleaseSection("v0.5.4","2026-10-13",new[]{"补齐 29 张中立 3～6 费牌，经典池所有 3～6 费卡可用，总支持 380 / 382。","新增死亡后与出牌结算后公共触发，苦痛、教派领袖、食尸鬼、教师、加基森与伊利丹走独立规则注册。","无面显式深复制属性和附加效果；报警机器人交换不触发战吼；恐怖海盗按武器攻击动态减费。","格尔宾四种发明与酋长三种和弦全部可用，保持七槽上限、手牌隐私和冻结经典数值。","办公窗口与特效开关保持不变，专项检查可按 mid-cost / high-cost 选择。"}));
  body.Children.Add(ReleaseSection("v0.5.3","2026-10-12",new[]{"补齐 12 张中立 1 / 2 费牌，兼容合并猎人职业牌，总支持 351 / 382。","猎人经典 25 / 25 可用：追踪术私下选牌、野兽召唤与死亡、武器和法术效果；保留当前奥秘与扩展接口。","新增角色治疗、召唤后与施法后公共触发，包含飞刀、炎术师、圣光护卫者、魔瘾者等；战吼和触发按阶段结算。","狂野怒火攻击与免疫在回合结束失效；追踪候选只给本人，不泄露到共享记录。"}));
  body.Children.Add(ReleaseSection("v0.5.2","2026-10-12",new[]{"法师、圣骑士、萨满和术士各 25 / 25 张经典职业牌可用，总支持 328 / 382。","补齐冰锥术、安东尼达斯、肯瑞托法师、法力浮龙、公正之剑、先祖之魂、视界术、妖术等剩余效果。","加拉克苏斯替换为 15 血上限、3/8 血怒和地狱火技能；英雄与技能接口独立，指定登场台词仅普通且特效全开显示。","先祖之魂接入公共附加亡语，按位置和施加顺序结算；变形不触发召唤，保持手牌隐私、办公窗口与按需测试。"}));
  body.Children.Add(ReleaseSection("v0.5.1","2026-10-12",new[]{"经典牧师全部 25 张职业牌可用，总支持 314 / 382；补齐复制、控场、北郡、光明之泉、光耀之子、奥金尼、维伦、暗影形态与圣殿执行者。","治疗按来源统一结算，维伦翻倍法术和技能，奥金尼转换治疗；偷来的职业随从和武器可在对局使用，组牌仍绑定职业。","局域网随机决定先后手，固定己方视角保留；起手、幸运币、首回合抽牌与法力按实际先后手分配。","暗影步保持回手减两费、重新打出后清除的原规则；不跨登场累计。"}));
  body.Children.Add(ReleaseSection("v0.5.0","2026-10-12",new[]{"扩展接口与按需测试重构，保留 302 / 382 张已支持卡牌和办公界面。","规则测试独立运行，按模块与改动选择；界面、TCP 与发布完整检查分层调用。","集中模式配置、附加效果与触发注册；公开状态使用白名单，局域网协议 2 按确认序号增量同步记录。"}));
  body.Children.Add(ReleaseSection("v0.4.7","2026-10-12",new[]{"经典战士全部 25 张职业牌及 15 张经典奥秘可用，支持 302 / 382。","新增受伤触发、命令怒吼、战歌冲锋和武器规则；奥秘匿名挂出、英雄问号标记，触发时公开并按顺序结算。"}));
  body.Children.Add(ReleaseSection("v0.4.6","2026-10-10",new[]{"盗贼全部 25 张职业牌和鱼人杀手蟹可用，支持 276 / 382。","支持暗影步手牌减费、伺机待发、临时潜行、剑刃乱舞、背叛、耐心刺客和消失；满手牌回手会销毁并处理亡语与墓地。"}));
  body.Children.Add(ReleaseSection("v0.4.5","2026-10-10",new[]{"后台墓地记录死亡随从、已施放法术和其他离场原因，为后续复活预留；不增加查看入口。","合并四张鱼人牌：寒光智者、寒光先知、老瞎眼与鱼人招潮者，支持 267 / 382。","更新日志按 v0.4.0、v0.3.0 等系列折叠，展开后查看各版本详情。","普通模式开启特效时，加成后的攻击显示绿色，实际受伤的生命显示红色；设为 1 生命上限不视为受伤。"}));
  body.Children.Add(ReleaseSection("v0.4.4","2026-10-10",new[]{"术士除加拉克苏斯大王外均可用，支持 263 / 382。","艾德温连击达到 12 攻时，特效全开可震屏两秒，联机双方同步等待。","经典八张连击牌全部可用；连击目标、出牌计数和裂颅之击延迟回手统一结算。","瓦莉拉开场白改为“当心你的背后”。","普通模式开启特效时，英雄武器槽和随从冻结关键词显示冰蓝色；关闭特效和极简不启用标记。","公开记录新增英雄冻结提示；水元素、护甲、反击、风怒、解冻和局域网冻结攻击拦截专项通过。"}));
  body.Children.Add(ReleaseSection("v0.4.3","2026-10-10",new[]{"组牌页可直接下拉选择已保存卡组进行编辑，保存更新原卡组。","修复先手换牌后首回合漏抽牌：先手三张加回合抽一张，后手四张加幸运币。","补齐经典德鲁伊全部法术、九张过载牌，支持 247 / 382；显示待锁与已锁水晶。","特效全开时向对手展示完整法术信息，停留两秒后淡出，多张按序排列；本地 test 可预览双方施法，极简禁用。","修复对方对话取消攻击、法术、技能及抉择位置选择。","卡组代码保存在 Windows 用户数据目录，不再依赖程序安装位置；更新或更换程序目录后仍可读取。","首次启动自动读取并合并旧版本程序目录里的卡组代码；卡组草稿与卡组库分开保存。","从已保存卡组切换职业会新建独立卡组；组卡页支持二次确认后删除指定已保存卡组。"}));
  body.Children.Add(ReleaseSection("v0.4.2","2026-10-10",new[]{"新增米尔豪斯·法力风暴、纳特·帕格、游学者周卓、任务达人、末日预言者、憎恶、长鬃草原狮和比斯巨兽，支持数量增至 243 / 382。","条件触发、回合开始与限时费用分别使用公共机制；手中法术允许跨职业使用，组牌限制与未实现效果拦截保留。","合并右键英雄对话、自动开场与落败台词和种族标注；保留无边框、极简禁用对话及随从台词特效开关。"}));
  body.Children.Add(ReleaseSection("v0.4.1","2026-10-09",new[]{"武器槽显示英雄总攻击力，德鲁伊可从此发起攻击。","组牌品质颜色随特效设置切换；普通模式英雄对话显示气泡。","死亡之翼开启特效时震屏两秒并飘过红色火字；任意一方启用时双方同步暂停操作，关闭特效者显示正在播放动画。"}));
  body.Children.Add(ReleaseSection("v0.4.0","2026-10-09",new[]{"合并双方功能分支，经典支持数量增至 235 / 382。","增加起手换牌，双方确认后开始对局，后手获得幸运币；英雄对话通过房主校验并共享。","补齐回合结束触发、临时控制返还、到期销毁及伊瑟拉和五张梦境牌；霍格统一为单一触发。","新增六张圣骑士卡及哈里森、凯恩、希尔瓦娜斯和火车王；组牌页显示品质。","保留光环、三巨人、左右召唤和底部抉择；修复换牌抽回原牌、神圣愤怒英雄目标与永久控制后回手归属。"}));
  body.Children.Add(ReleaseSection("v0.3.6",AppRelease.Date,new[]{"补齐经典十四张光环牌的持续属性、冲锋、减费与加费效果；来源沉默或离场后动态移除，手牌展示实际费用。","支持鱼人双方光环、相邻位置更新和生命光环移除后的生命上限调整。实现经典山岭巨人、海巨人、熔核巨人的动态费用，减费条件变化时同步更新手牌和实际扣费。奥妮克希亚左右交替召唤雏龙补满七随从；空场左右各三只。新增十张经典抉择牌，先选效果再验证目标；右键取消保留手牌，不改变牌库。可对战牌为 211 / 382。"}));
  body.Children.Add(ReleaseSection("v0.3.5","2026-10-09",new[]{"组牌增加职业与中立筛选，新增鱼人猎潮者、剃刀猎手和白银之手骑士召唤战吼。","普通模式弃牌后显示牌名飘字，持续 2.5 秒，不受“特效全开”开关影响；多张弃牌分行显示。","弃牌飘字通过公开结构化事件同步双方并去重，极简模式不显示。","本地 test 无需满 30 张牌即可开局，职业、数量上限与效果支持限制保留；局域网仍要求完整卡组。"}));
  body.Children.Add(ReleaseSection("v0.3.4","2026-10-09",new[]{"卡组绑定职业，选择已保存卡组或当前编辑卡组后自动切换并锁定职业；基础练习卡组按所选职业生成。","出牌与 test 取牌增加职业限制，只能使用本职业和中立牌；卡组导入、导出和开局继续校验职业。","组牌关键词搜索整个卡池，其他职业牌可查看但不能加入；支持多关键词，无结果时提示检查费用与类型筛选。"}));
  body.Children.Add(ReleaseSection("v0.3.3","2026-10-09",new[]{"增加统一手牌弃牌结算，支持灵魂之火、魔犬、末日守卫和死亡之翼；随机弃牌不重复选择，手牌不足时弃掉现有牌。","房主结算随机结果，双方共享被弃掉的牌名，剩余手牌保密；死亡之翼的亡语抽牌在弃牌完成后结算。","可对战卡牌增加至 183 / 382；追踪术仍待牌库选择机制实现。"}));
  body.Children.Add(ReleaseSection("v0.3.2","2026-10-09",new[]{
   "新增经典六张激怒牌，受伤时获得攻击、风怒或武器加攻；完全治疗、沉默或离场后移除对应加成。对战支持数量为 180 / 382。",
   "设置增加“特效全开”：普通模式可启用金色传说随从名字、生效中的红色激怒标记和震动，默认关闭。",
   "普通模式始终显示法术提示与伤害飘字，伤害飘字延长至 1 秒；极简模式不显示任何特效。",
   "更新日志按版本折叠，点击版本号展开详细内容。"}));
  body.Children.Add(ReleaseSection("v0.3.1","2026-10-09",new[]{"本地 test 双方开局 10 个满水晶；小“取”按钮从己方剩余牌库指定取牌，手牌上限十张。","普通模式伤害飘字延长至 0.8 秒。"}));
  body.Children.Add(ReleaseSection("v0.3.0","2026-10-09",new[]{"普通模式新增双方共享的法术提示、伤害飘字；单次伤害大于3震动目标，大于10同时震动整个界面。","视觉事件独立于规则与记录，经局域网同步并去重；极简模式不播放动画。","检查更新优先读取 GitHub 版本订阅，失败时使用 API，改善403错误说明。","运行目录仅保留 Touchfish.exe 和 Touchfish.Update.exe，清理已结束的临时更新器。"}));
  body.Children.Add(ReleaseSection("v0.2.0","2026-10-08",new[]{"接入 GitHub Releases：检查更新、下载校验、安装重启与历史版本回退。","个人卡组、草稿和设置保留；本地不长期保留旧版，历史版本仅存于 GitHub。","增加独立更新器，旧版回退后仍可重新升级；对局中阻止安装。"}));
  body.Children.Add(ReleaseSection("v0.1.1","2026-10-08",new[]{"修复玛里苟斯法术伤害加成，由 +1 改为 +5。","显示对手手牌数量，保留内容隐藏。"}));
  body.Children.Add(ReleaseSection("v0.1","2026-10-08",AppRelease.Notes));
  GroupReleaseVersions(body);
  Put(releasePanel,new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled},1);

 }
 void GroupReleaseVersions(StackPanel body){var versions=body.Children.OfType<Expander>().ToArray();body.Children.Clear();var groups=new Dictionary<string,StackPanel>();foreach(var version in versions){string[] parts=((string)version.Tag).Split('.');string series=parts[0]+"."+parts[1]+".0";StackPanel content;if(!groups.TryGetValue(series,out content)){content=new StackPanel{Margin=new Thickness(8,4,0,0)};groups[series]=content;body.Children.Add(new Expander{Tag=series,Header=T(series,11,Ink),Content=content,IsExpanded=false,FontWeight=FontWeights.Normal,Foreground=Ink,HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,0,0,6)});}content.Children.Add(version);}}
 Expander ReleaseSection(string version,string date,string[] notes){var content=new StackPanel{Margin=new Thickness(18,4,4,6)};content.Children.Add(T(date,9,Muted));foreach(string note in notes){var text=T(note,10,Ink);text.TextWrapping=TextWrapping.Wrap;text.Margin=new Thickness(0,4,0,4);content.Children.Add(text);}return new Expander{Tag=version,Header=T(version,11,Ink),Content=content,IsExpanded=false,FontWeight=FontWeights.Normal,Foreground=Ink,Margin=new Thickness(0,0,0,6),HorizontalContentAlignment=HorizontalAlignment.Stretch};}
 void RenderExpandedRelease(string folder){var scroll=(ScrollViewer)releasePanel.Children[1];var body=(StackPanel)scroll.Content;var first=(Expander)body.Children[0];first.IsExpanded=true;var versions=first.Content as StackPanel;var latest=versions==null?null:versions.Children.OfType<Expander>().FirstOrDefault();if(latest!=null)latest.IsExpanded=true;Render(System.IO.Path.Combine(folder,"62-release-expanded.png"));if(latest!=null)latest.IsExpanded=false;first.IsExpanded=false;}
 void OpenReleaseNotes(){ApplyFonts(releasePanel);ShowPage("release");}
}
}
