using BazaarPlusPlus.Android;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var state = new AndroidPreviewGestureState();
Require(!state.Begin(1, true, false), "Standard input cannot activate preview.");
Require(!state.Begin(1, false, true), "A hidden dock cannot activate preview.");
Require(state.Begin(1, true, true), "Advanced hold starts preview.");
Require(!state.Begin(2, true, true), "A second finger cannot replace the holder.");
Require(!state.ContactDown(1), "The holder cannot preview itself.");
Require(state.ContactDown(2), "A second finger previews while held.");
state.ContactUp(2);
Require(state.Active && state.ShieldRequired, "Lifting the inspecting finger preserves the hold.");
Require(state.ContactDown(3) && state.ContactDown(4), "Several inspection fingers are consumed.");
state.Stop();
Require(!state.Active && state.ShieldRequired, "Releasing the holder ends preview but drains existing contacts.");
Require(!state.ContactDown(5), "New contacts during draining cannot preview.");
state.ContactUp(99);
state.ContactUp(3);
state.ContactUp(4);
Require(state.ShieldRequired, "All consumed contacts must end before input is restored.");
Require(!state.Begin(6, true, true), "Draining cannot be reactivated prematurely.");
state.ContactUp(5);
Require(!state.ShieldRequired, "Input is restored after the final contact ends.");
Require(state.Begin(6, true, true), "Another hold works after draining.");
state.ContactDown(7);
state.Cancel();
Require(!state.Active && !state.ShieldRequired, "Focus cancellation clears stale contacts.");
Require(state.Begin(8, true, true), "Focus recovery permits a fresh hold.");
state.Stop();
Require(!state.ShieldRequired, "A hold with no other fingers releases immediately.");
Console.WriteLine("Hold preview mode gating, pointer ownership, release ordering and cancellation passed.");
