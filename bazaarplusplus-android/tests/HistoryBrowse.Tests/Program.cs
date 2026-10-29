using System.Text.Json;
using BazaarPlusPlus.Android;

var active = JsonSerializer.SerializeToElement(new {hero="Pygmalien",playMode="Unranked",status="active",startedAt=1000L,endedAt=(long?)null,day=1,victories=0,losses=0,battleCount=0,startDay=1});
Check(HistoryViewText.Date(active,"endedAt")=="未记录" && HistoryViewText.Run(active).Contains("普通 · 进行中"),"active run with null ending remains browsable");
var ranked = JsonSerializer.SerializeToElement(new {hero="Vanessa",playMode="Ranked",status="completed",startedAt=1000L,endedAt=2000L,day=8,victories=6,losses=4,battleCount=16,startDay=1});
Check(HistoryViewText.Run(ranked).Contains("排位 · 已结束") && HistoryViewText.Run(ranked).Contains("6 胜 / 4 负"),"ranked and casual records share the same history presentation");
var missing = JsonSerializer.SerializeToElement(new {status="completed",reason="final_progress_unavailable",day=7,victories=2,losses=6});
Check(HistoryViewText.Run(missing).Contains("最终进度未记录") && HistoryViewText.Run(missing).Contains("最后观察"),"unavailable final totals cannot be presented as final facts");
var empty = JsonSerializer.SerializeToElement(new {status="captured",cards=Array.Empty<object>()});
var failed = JsonSerializer.SerializeToElement(new {status="failed",cards=Array.Empty<object>()});
Check(HistoryViewText.Group(empty).Contains("完整采集，空阵容") && HistoryViewText.Group(failed)=="采集失败","captured empty and failed formation remain distinct");
var unknown = JsonSerializer.SerializeToElement(new {sequence=3,kind="pvp",day=4,hour=2,outcome=(string?)null});
Check(HistoryViewText.Battle(unknown).Contains("结果未记录") && !HistoryViewText.Battle(unknown).Contains("· 平"),"missing outcome never becomes a draw");
var card = JsonSerializer.SerializeToElement(new {name="旧卡牌 <sprite name=x>",tier="Bronze",enchantment="Toxic",attributes=new {DamageAmount=70,PoisonApplyAmount=7}});
Check(HistoryViewText.Fields(card).Contains("DamageAmount：70") && HistoryViewText.Fields(card).Contains("PoisonApplyAmount：7"),"saved attributes remain exact without recalculating from a current template");
var named = JsonSerializer.SerializeToElement(new {kind="pvp",day=2,hour=6,sequence=1,outcome="win",opponentName="玩家🐸",opponentHero="Vanessa"});
Check(HistoryViewText.Battle(named).Contains("玩家🐸 · Vanessa"),"PvP list identifies the actual opponent");
var legacy = JsonSerializer.SerializeToElement(new {kind="pvp",opening=new {opponentHero="Pygmalien"}});
Check(HistoryViewText.Battle(legacy).Contains("玩家名称未记录 · Pygmalien"),"Legacy battles remain browsable without invented player names");
Check(HistoryFormationLayout.ItemSlot("Socket_0",9)==0 && HistoryFormationLayout.ItemSlot("Socket_2",0)==2,
    "Zero-based saved slots keep a medium item at 0 and its neighbour at 2 without overlap");
Check(HistoryFormationLayout.ItemSlot("OpponentSocket_9",0)==9 && HistoryFormationLayout.ItemSlot("未记录",4)==4
    && HistoryFormationLayout.ItemSlot("Socket_-1",4)==4 && HistoryFormationLayout.ItemSlot("Socket_128",4)==4,
    "Opponent positions are preserved and unavailable or invalid positions use sequential placement");
foreach (var (wins, losses, tier) in new[] { (0,3,"未达奖杯"), (3,3,"未达奖杯"), (4,3,"青铜胜利"), (6,3,"青铜胜利"),
    (7,3,"白银胜利"), (9,3,"白银胜利"), (10,1,"黄金胜利"), (10,0,"钻石胜利") })
{
    var row = JsonSerializer.SerializeToElement(new { status="completed", victories=wins, losses });
    Check(HistoryViewText.Tier(row)==tier, "Trophy boundaries match the saved final progress");
}
foreach (var json in new[] { "{\"status\":\"completed\",\"victories\":10}",
    "{\"status\":\"completed\",\"victories\":10,\"losses\":null}",
    "{\"status\":\"completed\",\"victories\":10,\"losses\":-1}",
    "{\"status\":\"completed\",\"victories\":10,\"losses\":\"0\"}" })
    Check(HistoryViewText.Trophy(JsonSerializer.Deserialize<JsonElement>(json))==null, "Unknown or invalid losses never imply diamond");
Check(HistoryViewText.Trophy(active)==null && HistoryViewText.Trophy(missing)==null,
    "Active and unavailable final progress never receive final trophies");
var interrupted=JsonSerializer.SerializeToElement(new {status="abandoned",reason="new_run_confirmed",victories=10,losses=0});
Check(HistoryViewText.Tier(interrupted)=="已中断" && HistoryViewText.Trophy(interrupted)==null && HistoryViewText.Badge(interrupted)=="state:pending",
    "An interrupted archive is not classified as surrender or victory");
Check(HistoryViewText.OpponentName(legacy)=="玩家名称未记录" && HistoryViewText.Value(HistoryViewText.Opponent(legacy),"opponentHero")=="Pygmalien",
    "Portrait identity resolves both list and nested detail metadata");
Check(HistoryViewText.Badge(missing)=="state:pending", "Incomplete final progress does not receive a broken-cup final result");
Console.WriteLine("History browser presentation and outcome boundaries: 26 checks passed.");
void Check(bool value,string message) { if(!value)throw new Exception(message); }
