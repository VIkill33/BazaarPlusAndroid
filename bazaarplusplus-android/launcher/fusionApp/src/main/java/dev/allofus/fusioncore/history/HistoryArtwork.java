package dev.allofus.fusioncore.history;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.os.Handler;
import android.os.Looper;
import android.util.LruCache;
import android.widget.ImageView;
import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.concurrent.LinkedBlockingQueue;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

/** Local template artwork only. No network requests or writes to the history database. */
public final class HistoryArtwork implements AutoCloseable {
    private final File root;
    private final Handler main = new Handler(Looper.getMainLooper());
    private final ThreadPoolExecutor worker = new ThreadPoolExecutor(1, 1, 0, TimeUnit.MILLISECONDS,
        new LinkedBlockingQueue<>(640), new ThreadPoolExecutor.DiscardPolicy());
    private final LruCache<String, Bitmap> cache = new LruCache<String, Bitmap>(12 * 1024 * 1024) {
        @Override protected int sizeOf(String key, Bitmap value) { return value.getByteCount(); }
    };
    private volatile boolean closed;
    public HistoryArtwork(File gameRoot) { root = new File(gameRoot, "BepInEx/history-art/v2"); }
    public static String hero(String name) { return "hero-" + ("Hero8".equalsIgnoreCase(name) ? "TheDragons" : name); }
    public static String frame(String size, String tier) { return "frame-" + size + "-" + tier; }
    public void bind(ImageView view, String key) {
        view.setImageDrawable(null); view.setTag(key);
        load(key, loaded -> {
            if (key.equals(view.getTag())) { view.setImageBitmap(loaded); view.setContentDescription(key + " · 图片已加载"); }
        });
    }
    /** Delivers the cached bitmap on the main thread; missing or invalid images are never delivered. */
    public void load(String key, java.util.function.Consumer<Bitmap> result) {
        if (closed || key == null || !key.matches("[A-Za-z0-9_-]{1,100}")) return;
        worker.execute(() -> {
            try {
                File current = new File(root, "current.txt");
                if (!current.isFile() || current.length() > 128) return;
                String version = new String(Files.readAllBytes(current.toPath()), StandardCharsets.UTF_8).trim();
                if (!version.matches("[a-f0-9]{64}")) return;
                String identity = version + "/" + key;
                Bitmap bitmap = cache.get(identity);
                if (bitmap == null) {
                    File file = new File(new File(root, version), key + ".png");
                    if (!file.isFile() || file.length() > 2 * 1024 * 1024) return;
                    BitmapFactory.Options options = new BitmapFactory.Options(); options.inJustDecodeBounds = true;
                    BitmapFactory.decodeFile(file.getPath(), options);
                    if (options.outWidth <= 0 || options.outHeight <= 0 || options.outWidth > 1024 || options.outHeight > 1024) return;
                    options.inJustDecodeBounds = false;
                    bitmap = BitmapFactory.decodeFile(file.getPath(), options);
                    if (bitmap == null) return;
                    cache.put(identity, bitmap);
                }
                final Bitmap loaded = bitmap;
                main.post(() -> { if (!closed) result.accept(loaded); });
            } catch (Exception ignored) { /* Saved names and fields remain the fallback. */ }
        });
    }
    @Override public void close() { closed = true; worker.shutdownNow(); main.removeCallbacksAndMessages(null); cache.evictAll(); }
}
