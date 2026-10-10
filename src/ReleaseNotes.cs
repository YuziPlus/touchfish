using System;
using System.Windows;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;

namespace Touchfish {
public static class AppRelease {
 public const string Version="v0.4.5",Date="2026-10-10";
 public static readonly string[] PatchNotes={"日志按版本系列折叠，特效全开时显示攻击加成和受伤生命颜色。","合并四张经典鱼人牌，支持 267 / 382，修正寒光先知战吼结算。"};
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
  body.Children.Add(ReleaseSection("v0.4.5",AppRelease.Date,new[]{"合并四张鱼人牌：寒光智者、寒光先知、老瞎眼与鱼人招潮者，支持 267 / 382。","更新日志按 v0.4.0、v0.3.0 等系列折叠，展开后查看各版本详情。","普通模式开启特效时，加成后的攻击显示绿色，实际受伤的生命显示红色；设为 1 生命上限不视为受伤。"}));
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
