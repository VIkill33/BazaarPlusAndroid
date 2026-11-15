namespace BazaarPlusPlus.Android;

// Screen positions are supplied by the UI; only the owning pointer can finish a drag.
internal sealed class CustomBuildDragState
{
    private int? _pointer;
    private float _pressedAt, _x, _y;
    private bool _swiping;
    internal bool Dragging { get; private set; }
    internal bool Owns(int pointer) => _pointer == pointer;

    internal bool Press(int pointer, float time, float x, float y)
    {
        if (_pointer.HasValue) return false;
        _pointer = pointer; _pressedAt = time; _x = x; _y = y;
        _swiping = false; Dragging = false;
        return true;
    }

    internal bool Update(int pointer, float time, float x, float y)
    {
        if (!Owns(pointer) || _swiping) return false;
        if (Dragging) return true;
        if ((x - _x) * (x - _x) + (y - _y) * (y - _y) > 24 * 24) _swiping = true;
        else if (time - _pressedAt >= .35f) Dragging = true;
        return Dragging;
    }

    internal bool Release(int pointer)
    {
        if (!Owns(pointer)) return false;
        var commit = Dragging;
        Cancel();
        return commit;
    }

    internal void Cancel() { _pointer = null; Dragging = false; _swiping = false; }
}
