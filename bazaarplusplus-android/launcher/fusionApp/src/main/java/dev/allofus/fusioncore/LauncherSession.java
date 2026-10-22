package dev.allofus.fusioncore;

/** The acknowledgement is evidence of this launch only, never of login or game feature correctness. */
public final class LauncherSession {
    private LauncherSession() {}
    public static boolean matches(String requestedSession, String receivedSession,
                                  String expectedVersion, String receivedVersion,
                                  String expectedHash, String receivedHash,
                                  long startedAt, long loadedAt, boolean loaded, boolean installedValid) {
        return requestedSession != null && !requestedSession.isEmpty() && requestedSession.equals(receivedSession)
                && expectedVersion.equals(receivedVersion) && expectedHash.equals(receivedHash)
                && startedAt > 0 && loadedAt >= startedAt && loaded && installedValid;
    }
}
