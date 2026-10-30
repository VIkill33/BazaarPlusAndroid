package dev.allofus.fusioncore.history;

import android.content.Context;
import android.content.res.ColorStateList;
import android.graphics.Typeface;
import android.graphics.Canvas;
import android.graphics.drawable.Drawable;
import android.graphics.drawable.GradientDrawable;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.View;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;
import dev.allofus.fusioncore.R;
import java.util.Locale;
import org.json.JSONObject;

/** Native, accessible purchase tile backed exclusively by the local template artwork cache. */
public final class PurchaseCardView extends LinearLayout {
    public PurchaseCardView(Context context, HistoryArtwork artwork, JSONObject item, int rank,
            long maximum, boolean featured, Runnable details) {
        super(context);
        boolean horizontal = (featured || rank == 0) && getResources().getConfiguration().fontScale < 1.3f;
        setOrientation(horizontal ? HORIZONTAL : VERTICAL);
        setGravity(Gravity.CENTER_VERTICAL);
        setPadding(dp(8), dp(8), dp(8), dp(8));
        setBackground(surface(rank, featured));
        setClipToOutline(true);
        setClickable(true); setFocusable(true);
        android.util.TypedValue ripple = new android.util.TypedValue();
        context.getTheme().resolveAttribute(android.R.attr.selectableItemBackground, ripple, true);
        setForeground(context.getDrawable(ripple.resourceId));
        setOnClickListener(v -> details.run());

        FrameLayout picture = new FrameLayout(context);
        picture.setBackgroundColor(color(R.color.bpp_surface));
        TextView missing = label("图片未缓存", 12, false);
        missing.setGravity(Gravity.CENTER);
        picture.addView(missing, new FrameLayout.LayoutParams(-1, -1));
        ImageView image = new ImageView(context) {
            @Override protected void onDraw(Canvas canvas) {
                float aspect = HistoryBattleLayout.artworkAspect(item.optString("size"));
                Drawable art = getDrawable();
                if (aspect <= 0 || art == null) { super.onDraw(canvas); return; }
                // Cached material textures use different pixel proportions from the game's card window.
                // Match the single-battle history artwork aspect, then fit the complete card in the tile.
                int height = Math.min(getHeight(), Math.round(getWidth() / aspect));
                int width = Math.min(getWidth(), Math.round(height * aspect));
                int left = (getWidth() - width) / 2, top = (getHeight() - height) / 2;
                art.setBounds(left, top, left + width, top + height);
                art.draw(canvas);
            }
        };
        image.setScaleType(ImageView.ScaleType.FIT_CENTER);
        image.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        picture.addView(image, new FrameLayout.LayoutParams(-1, -1));
        artwork.load("card-" + item.optString("templateId"), bitmap -> {
            image.setImageBitmap(bitmap); missing.setVisibility(GONE);
        });
        if (rank > 0) {
            int edge = rank <= 3 ? (featured ? 28 : 24) : rank < 10 ? 20 : 22;
            FrameLayout.LayoutParams markSize = new FrameLayout.LayoutParams(dp(edge), dp(edge), Gravity.TOP | Gravity.START);
            markSize.setMargins(dp(4), dp(4), 0, 0);
            picture.addView(rankMark(rank, featured), markSize);
        }
        LinearLayout.LayoutParams pictureSize = horizontal
            ? new LinearLayout.LayoutParams(0, dp(rank == 0 ? 88 : 108), 1)
            : new LinearLayout.LayoutParams(-1, dp(featured ? 128 : 84));
        addView(picture, pictureSize);

        LinearLayout copy = new LinearLayout(context); copy.setOrientation(VERTICAL);
        copy.setPadding(horizontal ? dp(12) : dp(6), dp(4), dp(4), dp(2));
        addView(copy, horizontal ? new LinearLayout.LayoutParams(0, -2, 1) : new LinearLayout.LayoutParams(-1, -2));
        String name = item.optString("name");
        TextView title = label(name.isEmpty() ? "未记录名称" : name, featured ? 18 : 15, true);
        title.setMaxLines(featured ? 2 : 1); title.setEllipsize(TextUtils.TruncateAt.END);
        copy.addView(title);
        boolean ranking = rank > 0;
        long count = item.optLong("purchaseCount");
        TextView value = label(ranking ? number(count) + " 次购买" : number(item.optLong("price")) + " 金币", featured ? 19 : 16, true);
        value.setTextColor(color(R.color.bpp_accent)); copy.addView(value);
        if (ranking) {
            ProgressBar bar = new ProgressBar(context, null, android.R.attr.progressBarStyleHorizontal);
            bar.setMax(1000); bar.setProgress((int)Math.round(1000d * count / Math.max(1, maximum)));
            bar.setProgressTintList(ColorStateList.valueOf(color(rank <= 3 ? metalColor(rank) : R.color.bpp_accent)));
            bar.setProgressBackgroundTintList(ColorStateList.valueOf(color(R.color.bpp_history_line)));
            bar.setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
            LinearLayout.LayoutParams barSize = new LinearLayout.LayoutParams(-1, dp(4));
            barSize.topMargin = dp(4); barSize.bottomMargin = dp(4); copy.addView(bar, barSize);
            copy.addView(label(number(item.optLong("gold")) + " 金币 · " + number(item.optLong("runCount")) + " 局", 12, false));
        } else {
            copy.addView(label(tier(item.optString("tier")) + " · " + item.optString("hero", "未记录英雄"), 12, false));
            copy.addView(label("第 " + item.optLong("day") + " 天 · 第 " + item.optLong("hour") + " 小时", 12, false));
            copy.addView(label(java.text.DateFormat.getDateTimeInstance(java.text.DateFormat.SHORT, java.text.DateFormat.SHORT)
                .format(new java.util.Date(item.optLong("purchasedAt"))), 12, false));
        }
        setContentDescription((ranking ? "第 " + rank + " 名，" : "") + name + "，" + value.getText()
            + (ranking ? "，" + number(item.optLong("gold")) + " 金币，" + item.optLong("runCount") + " 局" : "，" + tier(item.optString("tier")) + "，" + item.optString("hero", "未记录英雄")
                + "，第 " + item.optLong("day") + " 天，第 " + item.optLong("hour") + " 小时，"
                + java.text.DateFormat.getDateTimeInstance(java.text.DateFormat.SHORT, java.text.DateFormat.SHORT)
                    .format(new java.util.Date(item.optLong("purchasedAt")))) + "，查看详情");
    }

    private View rankMark(int rank, boolean featured) {
        FrameLayout mark = new FrameLayout(getContext());
        GradientDrawable disc = new GradientDrawable();
        disc.setShape(GradientDrawable.OVAL);
        disc.setColor(color(R.color.bpp_rank_disc));
        disc.setStroke(dp(1), color(rank <= 3 ? metalColor(rank) : R.color.bpp_border));
        mark.setBackground(disc);
        if (rank <= 3) {
            ImageView crown = new ImageView(getContext());
            crown.setImageResource(R.drawable.bpp_crown);
            crown.setImageTintList(ColorStateList.valueOf(color(metalColor(rank))));
            crown.setScaleType(ImageView.ScaleType.FIT_CENTER);
            crown.setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);
            int inset = dp(featured ? 5 : 4);
            mark.setPadding(inset, inset, inset, inset);
            mark.addView(crown, new FrameLayout.LayoutParams(-1, -1));
        } else {
            TextView number = label(Integer.toString(rank), rank < 10 ? 11 : 10, true);
            number.setGravity(Gravity.CENTER);
            number.setIncludeFontPadding(false);
            mark.addView(number, new FrameLayout.LayoutParams(-1, -1, Gravity.CENTER));
        }
        return mark;
    }
    private int metalColor(int rank) {
        return rank == 1 ? R.color.bpp_rank_gold : rank == 2 ? R.color.bpp_rank_silver : R.color.bpp_rank_bronze;
    }
    private GradientDrawable surface(int rank, boolean featured) {
        GradientDrawable background = new GradientDrawable();
        background.setColor(color(R.color.bpp_surface)); background.setCornerRadius(dp(10));
        int stroke = rank == 1 || featured ? R.color.bpp_rank_gold : rank == 2 ? R.color.bpp_rank_silver
            : rank == 3 ? R.color.bpp_rank_bronze : R.color.bpp_border;
        background.setStroke(dp((rank > 0 && rank <= 3) || featured ? 2 : 1), color(stroke));
        return background;
    }
    private TextView label(String value, int size, boolean strong) {
        TextView text = new TextView(getContext()); text.setText(value); text.setTextSize(size);
        text.setTextColor(color(strong ? R.color.bpp_text : R.color.bpp_muted));
        text.setPadding(0, 0, 0, 0);
        if (strong) text.setTypeface(null, Typeface.BOLD);
        return text;
    }
    private int color(int id) { return getContext().getColor(id); }
    private int dp(int value) { return Math.round(value * getResources().getDisplayMetrics().density); }
    private static String number(long value) { return String.format(Locale.getDefault(), "%,d", value); }
    private static String tier(String value) {
        switch (value) { case "Bronze": return "青铜"; case "Silver": return "白银"; case "Gold": return "黄金";
            case "Diamond": return "钻石"; case "Legendary": return "传奇"; default: return value; }
    }
}
