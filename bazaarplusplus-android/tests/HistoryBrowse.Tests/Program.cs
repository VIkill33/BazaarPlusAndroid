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
var unknown = JsonSerializer.SerializeToElement(new {sequence=3,kind="pve",day=4,hour=2,outcome=(string?)null});
Check(HistoryViewText.Battle(unknown).Contains("结果未记录") && !HistoryViewText.Battle(unknown).Contains("· 平"),"missing outcome never becomes a draw");
var card = JsonSerializer.SerializeToElement(new {name="旧卡牌 <sprite name=x>",tier="Bronze",enchantment="Toxic",attributes=new {DamageAmount=70,PoisonApplyAmount=7}});
Check(HistoryViewText.Fields(card).Contains("DamageAmount：70") && HistoryViewText.Fields(card).Contains("PoisonApplyAmount：7"),"saved attributes remain exact without recalculating from a current template");
Console.WriteLine("History browser normal record presentation: 6 checks passed.");
void Check(bool value,string message) { if(!value)throw new Exception(message); }
