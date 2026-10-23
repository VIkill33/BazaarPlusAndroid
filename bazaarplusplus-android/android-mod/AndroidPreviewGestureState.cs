namespace BazaarPlusPlus.Android;

internal sealed class AndroidPreviewGestureState
{
    private int? _holder;
    private readonly HashSet<int> _contacts = new();
    internal bool Active => _holder.HasValue;
    internal bool ShieldRequired => Active || _contacts.Count > 0;
    internal bool IsHolder(int pointer) => _holder == pointer;

    internal bool Begin(int pointer, bool available, bool advanced)
    {
        if (!available || !advanced || ShieldRequired) return false;
        _holder = pointer;
        return true;
    }

    internal bool ContactDown(int pointer)
    {
        if (IsHolder(pointer)) return false;
        _contacts.Add(pointer);
        return Active;
    }

    internal void ContactUp(int pointer) => _contacts.Remove(pointer);
    internal void Stop() => _holder = null;
    internal void Cancel()
    {
        Stop();
        _contacts.Clear();
    }
}
