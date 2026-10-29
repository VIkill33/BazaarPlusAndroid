namespace BazaarPlusPlus.Android;

// The first presented simulation frame is at t=0, matching the native 50 ms frame cadence.
internal sealed class AndroidCombatClock
{
    internal bool Active { get; private set; }
    internal bool HasPlayback { get; private set; }
    internal long Generation { get; private set; }
    internal int Frames { get; private set; }
    internal long Begin()
    {
        Generation++;
        Active = HasPlayback = true;
        Frames = 0;
        return Generation;
    }
    internal void Advance() { if (Active && Frames < int.MaxValue) Frames++; }
    internal bool Complete(long generation, bool successful)
    {
        if (generation != Generation || !Active) return false;
        Active = false;
        if (!successful) HasPlayback = false;
        return true;
    }
    internal void Reset()
    {
        Generation++;
        Active = HasPlayback = false;
        Frames = 0;
    }
    internal string Text
    {
        get
        {
            var centiseconds = Math.Max(Frames - 1L, 0) * 5;
            return FormattableString.Invariant($"{centiseconds / 6000}:{centiseconds / 100 % 60:00}.{centiseconds % 100:00}");
        }
    }
}
