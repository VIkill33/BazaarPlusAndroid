package com.unity3d.player;

import android.app.Activity;
import android.content.res.Configuration;

/** Compile-only stand-in for the game's activity. Not packaged into the launcher. */
public class UnityPlayerActivity extends Activity {
    @Override
    public void onConfigurationChanged(Configuration newConfig) {
        super.onConfigurationChanged(newConfig);
    }
}
