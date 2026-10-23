import android.os.SystemClock;
import android.view.InputDevice;
import android.view.InputEvent;
import android.view.MotionEvent;
import java.lang.reflect.Method;

/** Two-pointer input probe, executed as shell via app_process on the target Android build. */
public class HoldGesture {
    private static boolean holderReleased;
    private static Object manager;
    private static Method inject;
    private static long start;

    private static void send(int action, int count, float hx, float hy, float tx, float ty)
            throws Exception {
        MotionEvent.PointerProperties[] properties = new MotionEvent.PointerProperties[count];
        MotionEvent.PointerCoords[] coordinates = new MotionEvent.PointerCoords[count];
        for (int i = 0; i < count; i++) {
            properties[i] = new MotionEvent.PointerProperties();
            properties[i].id = action == MotionEvent.ACTION_UP && count == 1 && holderReleased ? 1 : i;
            properties[i].toolType = MotionEvent.TOOL_TYPE_FINGER;
            coordinates[i] = new MotionEvent.PointerCoords();
            coordinates[i].x = i == 0 && !holderReleased ? hx : tx;
            coordinates[i].y = i == 0 && !holderReleased ? hy : ty;
            coordinates[i].pressure = 1;
            coordinates[i].size = 1;
        }
        MotionEvent event = MotionEvent.obtain(start, SystemClock.uptimeMillis(), action,
                count, properties, coordinates, 0, 0, 1, 1, 0, 0, InputDevice.SOURCE_TOUCHSCREEN, 0);
        try {
            if (!(Boolean) inject.invoke(manager, event, 2)) {
                throw new IllegalStateException("Input injection rejected");
            }
        } finally {
            event.recycle();
        }
    }

    public static void main(String[] args) throws Exception {
        Class<?> type = Class.forName("android.hardware.input.InputManagerGlobal");
        manager = type.getMethod("getInstance").invoke(null);
        inject = type.getMethod("injectInputEvent", InputEvent.class, int.class);
        float hx = Float.parseFloat(args[0]), hy = Float.parseFloat(args[1]);
        float tx = Float.parseFloat(args[2]), ty = Float.parseFloat(args[3]);
        start = SystemClock.uptimeMillis();
        send(MotionEvent.ACTION_DOWN, 1, hx, hy, tx, ty);
        try {
            SystemClock.sleep(1500);
            send(MotionEvent.ACTION_POINTER_DOWN | (1 << MotionEvent.ACTION_POINTER_INDEX_SHIFT),
                    2, hx, hy, tx, ty);
            SystemClock.sleep(150);
            if (args.length > 4 && args[4].equals("holder-first")) {
                send(MotionEvent.ACTION_POINTER_UP, 2, hx, hy, tx, ty);
                holderReleased = true;
            } else {
                send(MotionEvent.ACTION_POINTER_UP | (1 << MotionEvent.ACTION_POINTER_INDEX_SHIFT),
                        2, hx, hy, tx, ty);
            }
            if (args.length == 6) {
                SystemClock.sleep(3000);
                tx = Float.parseFloat(args[4]);
                ty = Float.parseFloat(args[5]);
                send(MotionEvent.ACTION_POINTER_DOWN | (1 << MotionEvent.ACTION_POINTER_INDEX_SHIFT),
                        2, hx, hy, tx, ty);
                SystemClock.sleep(150);
                send(MotionEvent.ACTION_POINTER_UP | (1 << MotionEvent.ACTION_POINTER_INDEX_SHIFT),
                        2, hx, hy, tx, ty);
                SystemClock.sleep(4000);
            } else {
                SystemClock.sleep(6000);
            }
        } finally {
            send(MotionEvent.ACTION_UP, 1, hx, hy, tx, ty);
        }
    }
}
