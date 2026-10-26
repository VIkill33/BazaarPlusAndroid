using BazaarPlusPlus.Android;
using System.Text.Json;
using TheBazaar;

var account=Guid.NewGuid(); var run=Guid.NewGuid().ToString(); var local=Guid.NewGuid().ToString();
ClientCache.Profile=new Profile { Value=new ProfileData { AccountId=account } };
Data.Run=new Run(); AppState.CurrentState=new State("ChoiceState"); AppState._cachedRunInitialized=new Initialized(run);
var observer=new AndroidRunObserver(IntPtr.Zero);
observer.Update();
var initial=Take("observeRun"); Bound(initial,local); await Pump();
var checkpoint=Take("observeRun"); Bound(checkpoint,local); await Pump();
Expect(observer.Begin("monster",HistoryPhase.Combat,new {},new {},1,3)==null
    && AndroidHistoryBridge.Calls.Count==0,"PvE never submits snapshots or quarantine diagnostics");
var first=observer.Begin("message",HistoryPhase.PvpCombat,new {},new {},1,3) ?? throw new Exception("Opening missing");
var opening=Take("beginBattle");
var duplicate=observer.Begin("message",HistoryPhase.PvpCombat,new {},new {},1,3);
Expect(ReferenceEquals(first,duplicate),"Duplicate original must track its successful retry task using the original ticket");
opening.Completion.SetResult(JsonSerializer.SerializeToElement(new {state="recorded"})); await Pump();
// Finish during the 0.5s poll interval after a native account/run switch.
ClientCache.Profile.Value!.AccountId=Guid.NewGuid(); AppState._cachedRunInitialized=new Initialized(Guid.NewGuid().ToString());
Data.Run.Victories=9; Data.Run.Day=10;
observer.Complete(first,"Player","Opponent",true);
Expect(!AndroidHistoryBridge.Calls.Any(call=>call.Request.GetProperty("op").GetString()=="resolveBattle"),"Old task cannot read new account's progress before the periodic identity poll");
while(AndroidHistoryBridge.Calls.TryDequeue(out var old)) old.Completion.SetResult(JsonSerializer.SerializeToElement(new {state="stale"}));
await Pump();
// Fresh observer: fail its checkpoint transaction, then preserve the exact original opening for retry.
observer=new AndroidRunObserver(IntPtr.Zero); observer.Update();
initial=Take("observeRun"); Bound(initial,local); await Pump();
checkpoint=Take("observeRun");
AppState.CurrentState=new State("CombatState"); observer.Observe();
var combatObservation=Take("observeRun");
first=observer.Begin("retry-message",HistoryPhase.PvpCombat,new {value="original"},new {},1,3) ?? throw new Exception("Retry opening missing");
opening=Take("beginBattle"); var window=opening.Request.GetProperty("windowId").GetString();
// Deliberately deliver dependent failures before the original prerequisite failure.
opening.Completion.SetException(new IOException("dependency blocked")); await Pump();
combatObservation.Completion.SetException(new IOException("dependency blocked")); await Pump();
checkpoint.Completion.SetException(new IOException("injected checkpoint failure")); await Pump();
observer.Retry();
var retriedCheckpoint=Take("observeRun");
Expect(retriedCheckpoint.Request.GetProperty("phase").GetString()=="choice", "Retry sorts by original submission, never failure notification order");
Bound(retriedCheckpoint,local);
var retriedCombat=Take("observeRun"); Bound(retriedCombat,local);
var retriedOpening=Take("beginBattle");
Expect(retriedOpening.Request.GetProperty("windowId").GetString()==window
    && retriedOpening.Request.GetProperty("opening").GetProperty("value").GetString()=="original", "Retry must preserve the original window and snapshot after a dependency fails");
retriedOpening.Completion.SetResult(JsonSerializer.SerializeToElement(new {state="recorded"}));
while(AndroidHistoryBridge.Calls.TryDequeue(out var remaining)) remaining.Completion.SetResult(JsonSerializer.SerializeToElement(new {state="stale"}));

// The ending screen can outlive both Data.Run and the game's initialization cache.
var endingAccount=Guid.NewGuid(); var endingRun=Guid.NewGuid().ToString();
ClientCache.Profile.Value.AccountId=endingAccount;
Data.Run=new Run {Day=7,Hour=6,Victories=2,Losses=6};
AppState.CurrentState=new State("ChoiceState"); AppState._cachedRunInitialized=new Initialized(endingRun);
observer=new AndroidRunObserver(IntPtr.Zero); observer.Update();
initial=Take("observeRun"); Bound(initial,local); await Pump();
checkpoint=Take("observeRun"); Bound(checkpoint,local); await Pump();
AppState.CurrentState=new State("PVPCombatState"); observer.Observe();
Bound(Take("observeRun"),local); await Pump();
var endingTicket=observer.Begin("ending-message",HistoryPhase.PvpCombat,new {},new {},7,6) ?? throw new Exception("Ending opening missing");
Take("beginBattle").Completion.SetResult(JsonSerializer.SerializeToElement(new {state="recorded"})); await Pump();
Data.Run=null; AppState._cachedRunInitialized=null; AppState.CurrentState=new State("EndRunDefeatState");
observer.Complete(endingTicket,"Opponent","Player",true);
var ending=Take("observeRun"); var endingResult=Take("resolveBattle");
Expect(ending.Request.GetProperty("nativeRunId").GetString()==endingRun
    && ending.Request.GetProperty("phase").GetString()=="defeat" && !ending.Request.GetProperty("progressAvailable").GetBoolean(),
    "Explicit ending retains the same proven identity and marks unavailable final progress");
Expect(endingResult.Request.GetProperty("runId").GetString()==local && endingResult.Request.GetProperty("outcome").GetString()=="loss"
    && endingResult.Request.GetProperty("day").GetUInt32()==7 && endingResult.Request.GetProperty("losses").GetUInt32()==6
    && !endingResult.Request.GetProperty("progressAvailable").GetBoolean(),
    "Last original task retains its actual outcome without reading zero or inventing final counts");
Bound(ending,local); endingResult.Completion.SetResult(JsonSerializer.SerializeToElement(new {state="resolved"})); await Pump();

var facts=new HistoryProgress(7,6,2,6,"Pygmalien","Unranked");
HistoryRunContinuity Proven()
{
 var continuity=new HistoryRunContinuity(); continuity.Resolve(endingAccount.ToString(),endingRun,HistoryPhase.PvpCombat,facts); return continuity;
}
var retained=Proven().Resolve(endingAccount.ToString(),null,HistoryPhase.Replay,null);
Expect(retained.NativeRunId==endingRun && !retained.ProgressAvailable && retained.Phase==HistoryPhase.Replay,
    "Replay retains proven identity without turning object loss into an end");
foreach(var phase in new[]{HistoryPhase.Victory,HistoryPhase.Defeat})
 Expect(Proven().Resolve(endingAccount.ToString(),null,phase,null).NativeRunId==endingRun,"Both explicit ending phases preserve continuity");
Expect(Proven().Resolve(Guid.NewGuid().ToString(),null,HistoryPhase.Defeat,null).NativeRunId==null,"Changed account cannot inherit an old ending");
Expect(Proven().Resolve(null,null,HistoryPhase.Defeat,null).NativeRunId==null,"Missing account cannot inherit an old ending");
Expect(Proven().Resolve(endingAccount.ToString(),Guid.NewGuid().ToString(),HistoryPhase.Defeat,null).NativeRunId==null,"Conflicting cached run cannot inherit an old ending");
var left=Proven();left.Resolve(endingAccount.ToString(),endingRun,HistoryPhase.Unknown,null);
Expect(left.Resolve(endingAccount.ToString(),null,HistoryPhase.Defeat,null).NativeRunId==null,"Leaving a known run phase invalidates continuity");
left=Proven();left.Resolve(endingAccount.ToString(),endingRun,HistoryPhase.Choice,null);
Expect(left.Resolve(endingAccount.ToString(),null,HistoryPhase.Defeat,null).NativeRunId==null,"Ordinary choice without native data cannot extend an old run");
var unpaired=Proven().Resolve(endingAccount.ToString(),null,HistoryPhase.Defeat,new HistoryProgress(99,99,99,99,"Other","Other"));
Expect(unpaired.Progress==facts && !unpaired.ProgressAvailable,"Unpaired native progress never becomes terminal facts");
Expect(new HistoryRunContinuity().Resolve(endingAccount.ToString(),endingRun,HistoryPhase.Defeat,null).NativeRunId==null,
    "An ending without a previous reliable observation cannot invent a binding");
// A purchase can precede the asynchronous run-binding acknowledgement.
Data.Run=new Run(); AppState.CurrentState=new State("EncounterState");
ClientCache.Profile.Value.AccountId=Guid.NewGuid(); AppState._cachedRunInitialized=new Initialized(Guid.NewGuid().ToString());
observer=new AndroidRunObserver(IntPtr.Zero); observer.Update(); initial=Take("observeRun");
var fact=new PurchaseFact("not-a-uuid",Guid.NewGuid().ToString(),"Test card","Item","Gold",uint.MaxValue);
observer.Purchase(fact);
Expect(AndroidHistoryBridge.Calls.IsEmpty,"First purchase waits for binding, rather than disappearing or using an old run");
Bound(initial,local); await Pump(); checkpoint=Take("observeRun"); Bound(checkpoint,local); await Pump();
var purchase=Take("recordPurchase");
Expect(purchase.Request.GetProperty("card").GetProperty("Price").GetUInt32()==uint.MaxValue
    && purchase.Request.GetProperty("runId").GetString()==local,"Immutable purchase retains the full uint price and original run");
var purchaseId=purchase.Request.GetProperty("purchaseId").GetString();
purchase.Completion.SetException(new IOException("injected purchase write failure")); await Pump();
UnityEngine.Time.realtimeSinceStartup=6; observer.Update();
var purchaseRetry=Take("recordPurchase");
Expect(purchaseRetry.Request.GetProperty("purchaseId").GetString()==purchaseId,"Automatic retry preserves the same purchase identity");
var retryObserve=Take("observeRun"); Bound(retryObserve,local);
purchaseRetry.Completion.SetResult(JsonSerializer.SerializeToElement(new {state="recorded"})); await Pump();
AppState.CurrentState=new State("ReplayState"); observer.Purchase(fact);
Expect(!AndroidHistoryBridge.Calls.Any(c=>c.Request.GetProperty("op").GetString()=="recordPurchase"),"Replay cannot create purchase rows");
while(AndroidHistoryBridge.Calls.TryDequeue(out var replayCall)) Bound(replayCall,local);
await Pump();

// A scope change before binding must not transfer a queued fact to the next account.
UnityEngine.Time.realtimeSinceStartup=0; AppState.CurrentState=new State("ChoiceState");
observer=new AndroidRunObserver(IntPtr.Zero); observer.Update(); initial=Take("observeRun"); observer.Purchase(fact);
ClientCache.Profile.Value.AccountId=Guid.NewGuid(); AppState._cachedRunInitialized=new Initialized(Guid.NewGuid().ToString());
observer.Observe(); var switched=Take("observeRun"); Bound(initial,local); Bound(switched,Guid.NewGuid().ToString()); await Pump();
checkpoint=Take("observeRun"); Bound(checkpoint,local); await Pump();
Expect(!AndroidHistoryBridge.Calls.Any(c=>c.Request.GetProperty("op").GetString()=="recordPurchase"),"Unbound old-account purchase is never attached to the new account");
Console.WriteLine("Observer history guards and purchase first-binding, immutable uint price, automatic retry, replay and account-switch checks passed.");

AndroidHistoryBridge.Call Take(string op)
{
 if(!AndroidHistoryBridge.Calls.TryDequeue(out var call) || call.Request.GetProperty("op").GetString()!=op)
  throw new Exception("Expected "+op);
 return call;
}
void Bound(AndroidHistoryBridge.Call call,string runId)=>call.Completion.SetResult(JsonSerializer.SerializeToElement(new {state="bound",runId,allowFirstPartial=false,evidence=Array.Empty<object>()}));
async Task Pump() { await Task.Delay(30); observer.Update(); }
void Expect(bool value,string message) {if(!value)throw new Exception(message);}
