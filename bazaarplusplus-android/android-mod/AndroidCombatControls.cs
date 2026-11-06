using System.Globalization;

namespace BazaarPlusPlus.Android;

// Speed and pause rules shared with the desktop combat status bar.
internal static class AndroidCombatControls
{
    internal static readonly float[] SpeedSteps = { .5f, .67f, 1f };

    internal static float? Step(float speed)
    {
        foreach (var step in SpeedSteps)
            if (Math.Abs(step - speed) < .0001f) return step;
        return null;
    }

    internal static float NextSpeed(float current)
    {
        var index = 0;
        for (var i = 1; i < SpeedSteps.Length; i++)
            if (Math.Abs(SpeedSteps[i] - current) < Math.Abs(SpeedSteps[index] - current)) index = i;
        return SpeedSteps[(index + 1) % SpeedSteps.Length];
    }

    // The game's own fast-forward (above 1x) is never slowed down.
    internal static bool Overrides(float requested) => requested <= 1.0001f;

    internal static string SpeedText(float speed) => speed.ToString("0.##", CultureInfo.InvariantCulture) + "x";

    // Canvas units; close to Android's 8dp touch slop on a 1080-unit-high canvas, so finger jitter stays a tap.
    private const float TapSlop = 24;
    internal static bool IsDrag(float dx, float dy) => dx * dx + dy * dy > TapSlop * TapSlop;

    // A pause the timer started must not outlive the combat or the visible button.
    internal static bool ReleasePause(bool pausedByTimer, bool active, bool shown) =>
        pausedByTimer && !(active && shown);
}
