package dev.allofus.fusioncore;

import android.content.Context;

/** Translate known low-level errors at the UI boundary; preserve unknown platform diagnostics. */
final class LauncherMessages {
    private LauncherMessages() {}

    static String processExit(Context context, int reason) {
        int resource;
        switch (reason) {
            case 4: resource = R.string.bpp_exit_crash; break;
            case 5: resource = R.string.bpp_exit_native_crash; break;
            case 6: resource = R.string.bpp_exit_anr; break;
            case 3: resource = R.string.bpp_exit_memory; break;
            case 1: resource = R.string.bpp_exit_self; break;
            case 2: resource = R.string.bpp_exit_system; break;
            case 10: case 11: resource = R.string.bpp_exit_user; break;
            default: resource = R.string.bpp_process_ended;
        }
        return context.getString(resource);
    }

    static String error(Context context, String message) {
        if (message == null || message.isEmpty()) return context.getString(R.string.bpp_preparation_failed_detail);
        int resource;
        switch (message) {
            case "文件完整性校验失败，请重试": resource = R.string.bpp_integrity_failed; break;
            case "解压目录不可读": resource = R.string.bpp_extraction_unreadable; break;
            case "已取消准备，可重新初始化；已校验的缓存会保留": resource = R.string.bpp_preparation_canceled; break;
            case "状态文件超出大小限制": resource = R.string.bpp_status_file_too_large; break;
            case "无法将运行代码设为只读": resource = R.string.bpp_error_code_not_readonly; break;
            case "无法创建运行目录": resource = R.string.bpp_error_directory_create; break;
            case "拒绝越界压缩文件": resource = R.string.bpp_error_archive_traversal; break;
            case "解压内容超出限制": resource = R.string.bpp_error_archive_too_large; break;
            case "拒绝清理符号链接": resource = R.string.bpp_error_cleanup_symlink; break;
            case "无法读取运行目录": resource = R.string.bpp_error_directory_read; break;
            case "无法清理派生文件": resource = R.string.bpp_error_cleanup_failed; break;
            default: return message;
        }
        return context.getString(resource);
    }
}
