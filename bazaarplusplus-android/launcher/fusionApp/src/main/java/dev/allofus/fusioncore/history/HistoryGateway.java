package dev.allofus.fusioncore.history;

import org.json.JSONObject;

/** Called only by the bounded managed writer, through the initialized native host. */
public final class HistoryGateway {
    private static HistoryStore writer;
    private static String writerSession;
    private HistoryGateway() { }

    public static synchronized String write(String payload) throws Exception {
        if (payload.getBytes(java.nio.charset.StandardCharsets.UTF_8).length > HistoryStore.MAX_PAYLOAD_BYTES)
            throw new IllegalArgumentException("历史写入数据超出限制");
        JSONObject request = new JSONObject(payload);
        String session = request.getString("sessionId");
        if (writer == null) {
            if (!"observeRun".equals(request.getString("op"))) throw new IllegalStateException("历史尚未绑定对局");
            writer = new HistoryStore(HistoryRuntime.gameRoot(), HistoryRuntime.targetPackage(), session);
            writerSession = session;
        }
        if (!session.equals(writerSession)) throw new IllegalStateException("历史进程会话已改变，请重启启动器");
        return writer.write(request).toString();
    }
}
