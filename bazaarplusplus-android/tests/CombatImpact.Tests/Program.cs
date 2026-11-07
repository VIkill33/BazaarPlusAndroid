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
    && CombatImpactReport.Kind("CardModifyAttribute") == CombatImpactKind.Attribute
    && CombatImpactReport.Kind("CardReload") == CombatImpactKind.Attribute
    && CombatImpactReport.Kind("PlayerMaxHealthIncrease") == CombatImpactKind.Attribute
    && CombatImpactReport.Kind("PlayerShieldRemove") == null, "Action mapping");
Check(CombatImpactReport.Displayable("DamageAmount") && !CombatImpactReport.Displayable("Custom_3")
    && !CombatImpactReport.Displayable(null), "Custom and unresolved attributes stay hidden");

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
Check(crowdText.Contains("A ×1") && crowdText.Contains("E ×1") && !crowdText.Contains("另"),
    "Every target line is listed: " + crowdText);

// Attribute changes, trigger sources and the received view.
var buffEntities = new Dictionary<string, CombatImpactEntity>(entities);
var buffs = new List<CombatImpactExecution>
{
    new("aura", "sword", CombatImpactKind.Attribute, 10, Attribute: "DamageAmount", TriggerId: "glass", Frame: 1),
    new("aura", "sword", CombatImpactKind.Attribute, 10, Attribute: "DamageAmount", TriggerId: "glass", Frame: 2),
    new("aura", "bow", CombatImpactKind.Attribute, 5, Attribute: "DamageAmount", TriggerId: "aura", Frame: 2),
    new("aura", "bow", CombatImpactKind.Attribute, -1000, Attribute: "CooldownMax", Frame: 3),
    new("sword", "sword", CombatImpactKind.Attribute, 7, Attribute: "DamageAmount", Frame: 4),
    new("glass", "sword", CombatImpactKind.Attribute, null, Attribute: "CritChance", Frame: 5),
    new("bow", "glass", CombatImpactKind.Attribute, 2, Attribute: "CritChance", Frame: 6),
    new("bow", "glass", CombatImpactKind.Attribute, -1, Attribute: "CritChance", Frame: 7),
};
var buffReport = new CombatImpactReport(buffEntities, buffs, totals, uses);
var auraGroups = buffReport.Caused("aura");
Check(auraGroups.Select(group => (group.Kind, group.Attribute)).SequenceEqual(
    [(CombatImpactKind.Attribute, (string?)"CooldownMax"), (CombatImpactKind.Attribute, "DamageAmount")]), "Attribute groups split by key");
var damageBuff = auraGroups[1];
Check(damageBuff.Count == 3 && damageBuff.Total == 25, "Attribute deltas sum");
Check(damageBuff.Lines.Select(line => (line.Name, line.Count, line.Value)).SequenceEqual(
    [("Sword", 2, (long?)20), ("Bow", 1, (long?)5)]), "Attribute targets");
Check(damageBuff.Triggers.Select(line => (line.Name, line.Count)).SequenceEqual([("Hourglass", 2)]),
    "Trigger sources count distinct frames and skip self triggers");
var triggerEntities = new Dictionary<string, CombatImpactEntity>(entities);
foreach (var c in "abcde")
    triggerEntities[c.ToString()] = new(c.ToString(), c.ToString().ToUpperInvariant(), CombatImpactEntityKind.Item);
var manyTriggers = "abcde".Select(c => new CombatImpactExecution("glass", "sword", CombatImpactKind.Haste, 1000,
    TriggerId: c.ToString(), Frame: 1)).ToList();
var triggerText = AndroidCombatImpactText.Build(new CombatImpactReport(triggerEntities, manyTriggers,
    new Dictionary<string, IReadOnlyDictionary<CombatImpactKind, int>>(), new Dictionary<string, int>()), "glass", false, true);
Check(triggerText.Contains("触发来源：") && triggerText.Contains("A ×1") && triggerText.Contains("E ×1")
    && triggerText.Contains("触发来源：\n"), "Many trigger sources each get a line: " + triggerText);
Check(buffReport.Caused("glass").Single().Total == null, "Unknown attribute delta hides total");
Check(buffReport.Caused("bow").Single(group => group.Kind == CombatImpactKind.Attribute).Mixed, "Mixed directions");
var swordIncoming = buffReport.Received("sword");
Check(swordIncoming.Select(group => group.Attribute).SequenceEqual(["CritChance", "DamageAmount"]), "Received attribute groups");
Check(swordIncoming[1].Count == 2 && swordIncoming[1].Total == 20
    && swordIncoming[1].Lines.Single() == new CombatImpactLine("Aura", 2, 20), "Self-applied effects are not received");

var buffText = AndroidCombatImpactText.Build(buffReport, "aura", false, true);
Check(buffText.Contains("伤害增加") && buffText.Contains("×3 · 总计 +25") && buffText.Contains("Sword ×2 · +20")
    && buffText.Contains("冷却时间</b>") && buffText.Contains("总计 -1s") && buffText.Contains("Bow ×1 · -1s") && buffText.Contains("触发来源：Hourglass ×2"),
    "Chinese attribute text: " + buffText);
var bowText = AndroidCombatImpactText.Build(buffReport, "bow", false, false);
Check(bowText.Contains("Crit Chance Change") && bowText.Contains("+1%") && bowText.Contains("Hourglass ×2 · +1%"),
    "English mixed percent text: " + bowText);
Check(AndroidCombatImpactText.AttributeLabel("BurnApplyAmount", 1, false, true) == "灼烧增加"
    && AndroidCombatImpactText.AttributeLabel("HealthMax", 5, false, true) == "最大生命值"
    && AndroidCombatImpactText.AttributeLabel("Mystery", 5, false, false) == "Attribute Change", "Attribute labels");

// Periodic ledger: burn/poison/regen ticks split by the stacks each source owns.
PeriodicSide Side(params PeriodicAdjustment[] adjustments)
{
    var side = new PeriodicSide();
    side.Adjustments.AddRange(adjustments);
    return side;
}
PeriodicSide Apply(PeriodicSide side, PeriodicStatus status, params string?[] sources)
{
    side.Applies[status] = sources.ToList();
    return side;
}
PeriodicSide Set(PeriodicSide side, PeriodicAttribute attribute, int previous, int current)
{
    side.Attributes[attribute] = new PeriodicTransition(previous, current);
    return side;
}
IReadOnlyDictionary<(string, CombatImpactKind), CombatImpactPeriodic> Ledger(params PeriodicSide[] opponentFrames) =>
    CombatImpactPeriodicLedger.Project(opponentFrames.Select(side => new PeriodicFrame(null, side)).ToArray(), (_, _) => 1);
var burnTick = new PeriodicAdjustment(PeriodicDamage.Burn, PeriodicPool.Health, -10);

var single = Ledger(
    Set(Apply(Side(), PeriodicStatus.Burn, "bow"), PeriodicAttribute.Burn, 0, 10),
    Set(Side(burnTick), PeriodicAttribute.Burn, 10, 9));
Check(single[("bow", CombatImpactKind.Burn)] == new CombatImpactPeriodic(10, 0), "Single burn owner takes the tick");

var shared = Ledger(
    Set(Apply(Side(), PeriodicStatus.Burn, "bow"), PeriodicAttribute.Burn, 0, 6),
    Set(Apply(Side(), PeriodicStatus.Burn, "torch"), PeriodicAttribute.Burn, 6, 10),
    Set(Side(burnTick), PeriodicAttribute.Burn, 10, 9),
    Set(Side(new PeriodicAdjustment(PeriodicDamage.Burn, PeriodicPool.Health, -9)), PeriodicAttribute.Burn, 9, 8));
Check(shared[("bow", CombatImpactKind.Burn)].Health + shared[("torch", CombatImpactKind.Burn)].Health == 19
    && shared[("bow", CombatImpactKind.Burn)].Health == 11, "Burn split by owned stacks across decay");

var lethal = Set(Set(Side(new PeriodicAdjustment(PeriodicDamage.Burn, PeriodicPool.Shield, -3),
        new PeriodicAdjustment(PeriodicDamage.Burn, PeriodicPool.Health, -8)),
    PeriodicAttribute.Health, 5, -3), PeriodicAttribute.Shield, 3, 0);
lethal.Died = true;
var overkill = Ledger(
    Set(Set(Set(Apply(Side(), PeriodicStatus.Burn, "bow"), PeriodicAttribute.Burn, 0, 20),
        PeriodicAttribute.Health, 5, 5), PeriodicAttribute.HealthMax, 100, 100),
    Set(lethal, PeriodicAttribute.Burn, 20, 19));
Check(overkill[("bow", CombatImpactKind.Burn)] == new CombatImpactPeriodic(5, 3), "Death frame caps burn health, keeps shield");

var poison = Ledger(
    Set(Apply(Side(), PeriodicStatus.Poison, "vial", null), PeriodicAttribute.Poison, 0, 8),
    Side(new PeriodicAdjustment(PeriodicDamage.Poison, PeriodicPool.Health, -8)));
Check(poison[("vial", CombatImpactKind.Poison)].Health == 4 && poison.Count == 1, "Unresolved applier keeps its share unbooked");

var regen = Ledger(
    Set(Set(Set(Apply(Side(), PeriodicStatus.Regen, "salve"), PeriodicAttribute.HealthRegen, 0, 5),
        PeriodicAttribute.Health, 98, 98), PeriodicAttribute.HealthMax, 100, 100),
    Set(Side(new PeriodicAdjustment(PeriodicDamage.Regen, PeriodicPool.Health, 5)), PeriodicAttribute.Health, 98, 100));
Check(regen[("salve", CombatImpactKind.Regen)] == new CombatImpactPeriodic(2, 0), "Overheal is not realized regen");

var periodicReport = new CombatImpactReport(entities, executions, totals, uses, single);
var bowBurn = periodicReport.Caused("bow").First(group => group.Kind == CombatImpactKind.Burn);
Check(bowBurn.Periodic == new CombatImpactPeriodic(10, 0), "Report attaches periodic impact");
var burnText = AndroidCombatImpactText.Build(periodicReport, "bow", false, true);
Check(burnText.Contains("实际伤害 10") && !burnText.Contains("消耗护盾"), "Periodic text: " + burnText);
var overkillText = AndroidCombatImpactText.Build(new CombatImpactReport(entities, executions, totals, uses, overkill), "bow", false, false);
Check(overkillText.Contains("Dealt 5 · Shield consumed 3"), "English periodic text: " + overkillText);

Console.WriteLine($"CombatImpact checks passed: {checks}");
