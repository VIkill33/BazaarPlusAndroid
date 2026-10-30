import android.app.Application;
import android.content.Context;
import android.content.ContextWrapper;
import android.os.Build;
import android.view.Display;
import android.view.WindowManager;
import dev.allofus.fusioncore.tools.CustomContextWrapper;

public final class LauncherContextTest {
    public static void main(String[] args) {
        for (int api : new int[] {26, 28, 29, 30, 34}) {
            Build.VERSION.SDK_INT = api;
            // Distinct displays detect accidental use of the game's package context or display 0.
            Display gameDisplay = new Display(17);
            Display hostDisplay = new Display(42);
            Context game = new Context() {
                @Override public Display getDisplay() { return gameDisplay; }
            };
            Context host = new Context() {
                @Override public Display getDisplay() {
                    if (api < 30) throw new AssertionError("public getDisplay unavailable on API " + api);
                    return hostDisplay;
                }
                @Override public Object getSystemService(String name) {
                    if (api >= 30) throw new AssertionError("new API should delegate directly");
                    if (!WINDOW_SERVICE.equals(name)) throw new AssertionError("wrong service");
                    return (WindowManager) () -> hostDisplay;
                }
            };
            CustomContextWrapper wrapper = new CustomContextWrapper(game, host, new Application());
            Context activity = new ContextWrapper(wrapper);
            // Same virtual call used by ActivityThread in Android 9, including repeated configuration changes.
            for (int change = 0; change < 3; change++) {
                if (activity.getDisplay().getDisplayId() != 42 || activity.getDisplay() != hostDisplay)
                    throw new AssertionError("lost host display on API " + api);
            }
            if (CustomContextWrapper.hostResources(activity) != host)
                throw new AssertionError("launcher strings would resolve against the game package on API " + api);
            if (CustomContextWrapper.hostResources(host) != host)
                throw new AssertionError("unwrapped host context changed on API " + api);
            if (CustomContextWrapper.hostResources(activity) == game)
                throw new AssertionError("game package selected for launcher strings on API " + api);
        }
        System.out.println("Launcher context display tests passed (API 26, 28, 29, 30, 34)");
    }
}
