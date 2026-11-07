using BazaarPlusPlus.Android;

var checks = 0;
void Check(bool condition, string reason) { checks++; if (!condition) throw new Exception(reason); }

var entities = new Dictionary<string, CombatImpactEntity>
{
    ["sword"] = new("sword", "Sword", CombatImpactEntityKind.Item),
    ["glass"] = new("glass", "Hourglass", CombatImpactEntityKind.Item),
    ["bow"] = new("bow", "Bow", CombatImpactEntityKind.Item),
    ["aura"] = new("aura", "Aura", CombatImpactEntityKind.Skill),
    [CombatImpactReport.PlayerId(true)] = new(CombatImpactReport.PlayerId(true), "Opponent", CombatImpactEntityKind.Player),
};
var opponent = CombatImpactReport.PlayerId(true);
var executions = new List<CombatImpactExecution>
{
    new("sword", opponent, CombatImpactKind.Damage, Critical: true),
    new("sword", opponent, CombatImpactKind.Damage),
    new("sword", opponent, CombatImpactKind.Damage, Critical: true),
    new("glass", "sword", CombatImpactKind.Haste, 1000),
    new("glass", "sword", CombatImpactKind.Haste, 1000),
    new("glass", "bow", CombatImpactKind.Haste, 1500),
    new("aura", "sword", CombatImpactKind.Haste, null),
    new("bow", opponent, CombatImpactKind.Burn),
};
var totals = new Dictionary<string, IReadOnlyDictionary<CombatImpactKind, int>>
{
    ["sword"] = new Dictionary<CombatImpactKind, int> { [CombatImpactKind.Damage] = 120 },
    // Native burn total without an observed execution still reports; shield zero is ignored.
    ["bow"] = new Dictionary<CombatImpactKind, int> { [CombatImpactKind.Burn] = 40, [CombatImpactKind.Poison] = 9, [CombatImpactKind.Shield] = 0 },
};
var uses = new Dictionary<string, int> { ["sword"] = 3, ["glass"] = 2 };
var report = new CombatImpactReport(entities, executions, totals, uses);

var sword = report.Caused("sword");
Check(sword.Count == 1 && sword[0].Kind == CombatImpactKind.Damage, "Sword causes damage only");
Check(sword[0].Count == 3 && sword[0].CriticalCount == 2, "Executions and crits counted");
Check(sword[0].Total == 120, "Native damage total wins over observation");
Check(sword[0].Lines.Count == 0, "Hero targets are not listed");
Check(report.UseCount("sword") == 3 && report.UseCount("aura") == 0, "Use counts");

var glass = report.Caused("glass");
Check(glass.Count == 1 && glass[0].Kind == CombatImpactKind.Haste && glass[0].Count == 3, "Haste executions");
Check(glass[0].Total == 3500, "Duration total sums configured values");
Check(glass[0].Lines.Select(line => (line.Name, line.Count, line.Value)).SequenceEqual(
    [("Sword", 2, (long?)2000), ("Bow", 1, (long?)1500)]), "Card targets ordered by count");

var aura = report.Caused("aura");
Check(aura.Count == 1 && aura[0].Total == null, "Unknown duration hides total");

var bow = report.Caused("bow");
Check(bow.Select(group => group.Kind).SequenceEqual([CombatImpactKind.Burn, CombatImpactKind.Poison]), "Native-only kinds included in order");
Check(bow[0].Count == 1 && bow[0].Total == 40 && bow[1].Count == 0 && bow[1].Total == 9, "Native totals and counts kept separate");

var received = report.Received("sword");
Check(received.Count == 1 && received[0].Kind == CombatImpactKind.Haste && received[0].Count == 3, "Sword received haste");
Check(received[0].Total == null, "Mixed known/unknown durations hide total");
Check(received[0].Lines.Select(line => (line.Name, line.Count, line.Value)).SequenceEqual(
    [("Hourglass", 2, (long?)2000), ("Aura", 1, (long?)null)]), "Received sources");
Check(report.Received("bow")[0].Total == 1500, "Known duration total");
Check(report.Received(opponent).Count == 0, "Hero targets omitted from received view");
Check(report.Caused("missing").Count == 0 && !report.Contains("missing") && report.Contains("sword"), "Unknown ids");
Check(CombatImpactReport.Empty.Caused("sword").Count == 0 && !CombatImpactReport.Empty.Contains("sword"), "Empty report");

Check(CombatImpactReport.Kind("PlayerDamage") == CombatImpactKind.Damage
    && CombatImpactReport.Kind("PlayerBurnApply") == CombatImpactKind.Burn
    && CombatImpactReport.Kind("PlayerPoisonApply") == CombatImpactKind.Poison
    && CombatImpactReport.Kind("PlayerHeal") == CombatImpactKind.Healing
    && CombatImpactReport.Kind("PlayerShieldApply") == CombatImpactKind.Shield
    && CombatImpactReport.Kind("PlayerRegenApply") == CombatImpactKind.Regen
    && CombatImpactReport.Kind("CardCharge") == CombatImpactKind.Charge
    && CombatImpactReport.Kind("CardHaste") == CombatImpactKind.Haste
    && CombatImpactReport.Kind("CardSlow") == CombatImpactKind.Slow
    && CombatImpactReport.Kind("CardFreeze") == CombatImpactKind.Freeze
    && CombatImpactReport.Kind("CardDestroy") == CombatImpactKind.Destroy
    && CombatImpactReport.Kind("CardModifyAttribute") == null
    && CombatImpactReport.Kind("PlayerShieldRemove") == null, "Action mapping");

// Crit evidence: only an unambiguous frame marks critical outcomes.
Check(CombatImpactReport.CriticalFrame(1, 1) == true, "Single crit execution");
Check(CombatImpactReport.CriticalFrame(1, 0) == false, "No crit adjustment");
Check(CombatImpactReport.CriticalFrame(2, 2) == true, "All executions crit");
Check(CombatImpactReport.CriticalFrame(2, 1) == null, "Ambiguous frame stays unknown");

Check(AndroidCombatImpactText.Value(CombatImpactKind.Haste, 1500) == "1.50s"
    && AndroidCombatImpactText.Value(CombatImpactKind.Freeze, 2000) == "2s", "Durations follow desktop formatter");
Check(AndroidCombatImpactText.Value(CombatImpactKind.Damage, 12345) == "12,345"
    && AndroidCombatImpactText.Value(CombatImpactKind.Damage, 2_500_000) == "2.5M", "Amounts follow desktop formatter");

var caused = AndroidCombatImpactText.Build(report, "sword", false, true);
Check(caused.Contains("使用 3 次") && caused.Contains("伤害") && caused.Contains("×3（暴击 2）") && caused.Contains("总计 120"),
    "Chinese caused text: " + caused);
var hasted = AndroidCombatImpactText.Build(report, "glass", false, false);
Check(hasted.Contains("Haste") && hasted.Contains("×3 · 3.50s total") && hasted.Contains("Sword ×2 · 2s")
    && hasted.Contains("Bow ×1 · 1.50s"), "English target lines: " + hasted);
var incoming = AndroidCombatImpactText.Build(report, "sword", true, true);
Check(incoming.Contains("加速") && incoming.Contains("Hourglass ×2 · 2s") && incoming.Contains("Aura ×1")
    && !incoming.Contains("总计"), "Chinese received text: " + incoming);
Check(AndroidCombatImpactText.Build(report, "aura", true, true).Contains("本场未受到其他卡牌的效果"), "Empty received text");
Check(AndroidCombatImpactText.Build(CombatImpactReport.Empty, "sword", false, false).Contains("No recorded effects"), "Empty caused text");

var many = new List<CombatImpactExecution>();
foreach (var target in new[] { "a", "b", "c", "d", "e" }) many.Add(new("glass", target, CombatImpactKind.Slow, 1000));
var crowd = new CombatImpactReport(
    "abcde".ToDictionary(c => c.ToString(), c => new CombatImpactEntity(c.ToString(), c.ToString().ToUpperInvariant(), CombatImpactEntityKind.Item))
        .Append(new("glass", entities["glass"])).ToDictionary(),
    many, new Dictionary<string, IReadOnlyDictionary<CombatImpactKind, int>>(), new Dictionary<string, int>());
var crowdText = AndroidCombatImpactText.Build(crowd, "glass", false, true);
Check(crowdText.Contains("A ×1") && crowdText.Contains("C ×1") && !crowdText.Contains("D ×1") && crowdText.Contains("另 2 张"),
    "At most three target lines: " + crowdText);

Console.WriteLine($"CombatImpact checks passed: {checks}");
