import android.os.SystemClock;
import android.view.InputDevice;
import android.view.InputEvent;
import android.view.MotionEvent;
import java.lang.reflect.Method;

/** Shell-only touch injection for native build-card long-press, drop and cancellation checks. */
public class BuildDragGesture {
    public static void main(String[] args) throws Exception {
        float x = Float.parseFloat(args[0]), y = Float.parseFloat(args[1]);
        float toX = Float.parseFloat(args[2]), toY = Float.parseFloat(args[3]);
        boolean cancel = args.length > 4 && args[4].equals("cancel");
        Class<?> type = Class.forName("android.hardware.input.InputManagerGlobal");
        Object manager = type.getMethod("getInstance").invoke(null);
        Method inject = type.getMethod("injectInputEvent", InputEvent.class, int.class);
        long start = SystemClock.uptimeMillis();
        send(manager, inject, start, MotionEvent.ACTION_DOWN, x, y);
        try {
            SystemClock.sleep(900);
            for (int step = 1; step <= 24; step++) {
                SystemClock.sleep(25);
                send(manager, inject, start, MotionEvent.ACTION_MOVE,
                        x + (toX - x) * step / 24, y + (toY - y) * step / 24);
            }
            SystemClock.sleep(1800); // Allows an evidence screenshot before release.
        } finally {
            send(manager, inject, start, cancel ? MotionEvent.ACTION_CANCEL : MotionEvent.ACTION_UP, toX, toY);
        }
    }

    private static void send(Object manager, Method inject, long start, int action, float x, float y) throws Exception {
        MotionEvent event = MotionEvent.obtain(start, SystemClock.uptimeMillis(), action, x, y, 0);
        event.setSource(InputDevice.SOURCE_TOUCHSCREEN);
        try {
            if (!(Boolean) inject.invoke(manager, event, 2)) throw new IllegalStateException("Touch injection rejected");
        } finally { event.recycle(); }
    }
}
