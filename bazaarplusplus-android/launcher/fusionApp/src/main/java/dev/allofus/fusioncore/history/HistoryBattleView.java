package dev.allofus.fusioncore.history;

import android.content.Context;
import android.graphics.Rect;
import android.graphics.Typeface;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ImageView;
import android.widget.TextView;
import java.util.ArrayList;
import java.util.List;
import org.json.JSONArray;
import org.json.JSONObject;

/** Image-only battle sheet. Every item has the same image height, with no card frames or popups. */
public final class HistoryBattleView extends ViewGroup {
    // The former skill image area was 88dp high; use one quarter of that edge length.
    private static final int SKILL_EDGE_DP = 22;
    private final HistoryArtwork artwork;
    private final TextView information, footnote;
    private final Side[] sides = new Side[2];
    private final java.util.Map<View, Rect> bounds = new java.util.HashMap<>();
    private final float density;
    private final java.util.Map<ImageView, Float> nativeItemAspects = new java.util.HashMap<>();
    private static final class Side {
        ImageView portrait;
        TextView name, itemsLabel, skillsLabel;
        final List<ImageView> items = new ArrayList<>(), skills = new ArrayList<>();
    }
    public HistoryBattleView(Context context, HistoryArtwork artwork, JSONObject battle, String label, Runnable openInformation) {
        super(context); this.artwork = artwork; density = getResources().getDisplayMetrics().density;
        setContentDescription("完整对战阵容 · 所有卡牌在一屏内展示");
        information = label(label, 14); information.setTypeface(null, Typeface.BOLD);
        information.setOnClickListener(v -> openInformation.run());
        footnote = label("模板插画仅供参考", 11);
        JSONObject opening = battle.optJSONObject("opening"), snapshots = battle.optJSONObject("snapshots");
        for (int side = 0; side < 2; side++) {
            Side data = sides[side] = new Side(); String who = side == 0 ? "我方" : "对方";
            String hero = opening == null ? "未记录" : opening.optString(side == 0 ? "playerHero" : "opponentHero", "未记录");
            data.portrait = image(HistoryArtwork.hero(hero));
            data.name = label(who + "：" + hero, 16); data.name.setTypeface(null, Typeface.BOLD);
            JSONObject items = snapshots == null ? null : snapshots.optJSONObject(side == 0 ? "playerHand" : "opponentHand");
            JSONObject skills = snapshots == null ? null : snapshots.optJSONObject(side == 0 ? "playerSkills" : "opponentSkills");
            data.itemsLabel = label(groupLabel(items, who + "物品"), 12);
            data.skillsLabel = label(groupLabel(skills, who + "技能"), 12);
            images(items, data.items, true); images(skills, data.skills, false);
        }
    }
    private TextView label(String value, int size) {
        TextView text = new TextView(getContext()); text.setText(value); text.setTextSize(size);
        text.setTextColor(0xfff7e7c8); text.setGravity(android.view.Gravity.CENTER_VERTICAL);
        text.setIncludeFontPadding(false);
        text.setSingleLine(true); text.setEllipsize(android.text.TextUtils.TruncateAt.END);
        text.setId(android.R.id.text1); addView(text); return text;
    }
    private ImageView image(String key) {
        ImageView image = new ImageView(getContext()); image.setScaleType(ImageView.ScaleType.FIT_CENTER);
        image.setContentDescription(key + " · 图片未缓存"); artwork.bind(image, key); addView(image); return image;
    }
    private static String groupLabel(JSONObject group, String name) {
        JSONArray cards = group == null ? null : group.optJSONArray("cards"); int count = cards == null ? 0 : cards.length();
        String state = group == null ? "unavailable" : group.optString("status");
        if (state.equals("captured")) return name;
        return name + " · " + (state.equals("partial") ? "部分采集，" + count + " 张" : state.equals("failed") ? "采集失败" : "未采集");
    }
    private void images(JSONObject group, List<ImageView> result, boolean items) {
        JSONArray cards = group == null ? null : group.optJSONArray("cards"); if (cards == null) return;
        for (int i = 0; i < cards.length(); i++) {
            JSONObject card = cards.optJSONObject(i); if (card == null) continue;
            ImageView art = image("card-" + card.optString("templateId"));
            float nativeAspect = items ? HistoryBattleLayout.artworkAspect(card.optString("size")) : 0;
            if (nativeAspect > 0) {
                nativeItemAspects.put(art, nativeAspect);
                // Game RawImage maps the complete material UV rectangle into this native body.
                // Reproduce that projection instead of treating the packed texture as a square card.
                art.setScaleType(ImageView.ScaleType.FIT_XY);
            }
            result.add(art);
        }
    }
    private float[] aspects(List<ImageView> images) {
        float[] result = new float[images.size()];
        for (int i = 0; i < result.length; i++) {
            Float nativeAspect = nativeItemAspects.get(images.get(i));
            if (nativeAspect != null) { result[i] = nativeAspect; continue; }
            android.graphics.drawable.Drawable drawable = images.get(i).getDrawable();
            result[i] = drawable == null || drawable.getIntrinsicHeight() <= 0 ? 1f
                : Math.max(.05f, drawable.getIntrinsicWidth() / (float)drawable.getIntrinsicHeight());
        }
        return result;
    }
    private void place(View view, int x, int y, int width, int height) {
        width = Math.max(1, width); height = Math.max(1, height);
        bounds.put(view, new Rect(x, y, x + width, y + height));
        view.measure(MeasureSpec.makeMeasureSpec(width, MeasureSpec.EXACTLY), MeasureSpec.makeMeasureSpec(height, MeasureSpec.EXACTLY));
    }
    @Override protected void onMeasure(int widthSpec, int heightSpec) {
        int width = MeasureSpec.getSize(widthSpec), height = MeasureSpec.getSize(heightSpec); setMeasuredDimension(width, height); bounds.clear();
        int meta = lineHeight(information) + Math.round(12 * density), footer = lineHeight(footnote) + Math.round(8 * density);
        place(information, 0, 0, width, meta); place(footnote, 0, height - footer, width, footer);
        boolean landscape = width > height;
        int gap = Math.min(Math.round(8 * density), Math.min(width, height) / 40);
        int boardWidth = landscape ? (width - gap) / 2 : width;
        int boardHeight = landscape ? height - meta - footer : (height - meta - footer - gap) / 2;
        // Match the portrait to the actual name line, including the user's font scale.
        // This header spans the full board width and never takes a column from the artwork.
        int hero = Math.max(lineHeight(sides[0].name), lineHeight(sides[1].name));
        int label = Math.max(lineHeight(sides[0].itemsLabel), lineHeight(sides[0].skillsLabel)) + Math.round(4 * density);
        int iconGap = Math.max(1, Math.round(4 * density));
        int skillEdge = Math.max(1, Math.min(Math.round(SKILL_EDGE_DP * density), boardHeight / 8));
        float[][] itemAspects = {aspects(sides[0].items), aspects(sides[1].items)};
        float[][] skillAspects = {aspects(sides[0].skills), aspects(sides[1].skills)};
        HistoryBattleLayout.Plan plan = HistoryBattleLayout.plan(boardWidth,
            Math.max(1, boardHeight - hero - label * 2), itemAspects, skillAspects, iconGap, skillEdge);
        iconGap = plan.gap; skillEdge = plan.skillEdge; int itemEdge = Math.max(1, plan.itemHeight);
        int nextTop = meta;
        for (int side = 0; side < 2; side++) {
            Side data = sides[side]; int x = landscape ? side * (boardWidth + gap) : 0;
            int y = landscape ? meta : nextTop;
            int nameGap = Math.round(6 * density);
            place(data.portrait, x, y, hero, hero); place(data.name, x + hero + nameGap, y, boardWidth - hero - nameGap, hero);
            place(data.itemsLabel, x, y + hero, boardWidth, label);
            int itemTop = y + hero + label;
            singleRow(data.items, itemAspects[side], x, itemTop, itemEdge, iconGap);
            int skillTop = itemTop + (data.items.isEmpty() ? 0 : itemEdge) + iconGap;
            place(data.skillsLabel, x, skillTop, boardWidth, label);
            rows(data.skills, skillAspects[side], x, skillTop + label, boardWidth, skillEdge, iconGap);
            // Stack actual content, rather than leaving the unused half-screen budget between sides.
            nextTop = skillTop + label
                + HistoryBattleLayout.rowsHeight(skillAspects[side], boardWidth, skillEdge, iconGap) + gap;
        }
    }
    private static int lineHeight(TextView text) {
        android.graphics.Paint.FontMetrics metrics = text.getPaint().getFontMetrics();
        return Math.max(1, (int)Math.ceil(metrics.descent - metrics.ascent));
    }
    private void singleRow(List<ImageView> images, float[] aspects, int x, int y, int height, int gap) {
        int left = x;
        for (int i = 0; i < images.size(); i++) {
            int width = Math.max(1, (int)Math.ceil(height * aspects[i]));
            place(images.get(i), left, y, width, height); left += width + gap;
        }
    }
    private void rows(List<ImageView> images, float[] aspects, int x, int y, int width, int size, int gap) {
        int left = x, top = y;
        for (int i = 0; i < images.size(); i++) {
            int imageWidth = Math.max(1, (int)Math.ceil(size * aspects[i]));
            if (left > x && left + imageWidth > x + width) { left = x; top += size + gap; }
            place(images.get(i), left, top, imageWidth, size); left += imageWidth + gap;
        }
    }
    @Override protected void onLayout(boolean changed, int left, int top, int right, int bottom) {
        for (java.util.Map.Entry<View, Rect> entry : bounds.entrySet()) {
            Rect rect = entry.getValue(); entry.getKey().layout(rect.left, rect.top, rect.right, rect.bottom);
        }
    }
}
