package dev.allofus.fusioncore.history;

import org.json.JSONObject;

/** Called only by the bounded managed writer, through the initialized native host. */
public final class HistoryGateway {
    private static HistoryStore writer;
    private static volatile String writerSession;
    private HistoryGateway() { }

    /** Reads stay independent of the writer's transaction/failure queue. No reader can request all accounts. */
    public static String read(String payload) throws Exception {
        if (payload.getBytes(java.nio.charset.StandardCharsets.UTF_8).length > 8192)
            throw new IllegalArgumentException("历史查询超出限制");
        JSONObject request = new JSONObject(payload);
        String session = request.getString("sessionId");
        String currentSession = writerSession;
        if (currentSession != null && !currentSession.equals(session)) throw new IllegalStateException("历史会话已改变");
        HistoryReader.Scope scope = request.isNull("accountId") || !request.has("accountId")
            ? HistoryReader.Scope.pending(session) : HistoryReader.Scope.account(request.getString("accountId"));
        return new HistoryReader(HistoryRuntime.gameRoot(), HistoryRuntime.targetPackage(), scope).read(request).toString();
    }

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
