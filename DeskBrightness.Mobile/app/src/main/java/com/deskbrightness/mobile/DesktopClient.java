package com.deskbrightness.mobile;

import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.Locale;

final class DesktopClient {
    private static final String HOST = "127.0.0.1";
    private static final int PORT = 8765;
    private static volatile long lastSuccessAt = 0L;
    private static Socket socket;
    private static DataOutputStream output;
    private static DataInputStream input;

    private DesktopClient() {
    }

    public static boolean isConnected() {
        long last = lastSuccessAt;
        return last > 0 && (System.currentTimeMillis() - last) < 5000L;
    }

    public static String sendLux(float lux) {
        return sendPayload(
                String.format(Locale.US, "{\"lux\":%.4f,\"source\":\"lightSensor\"}", lux)
        );
    }

    public static String sendBrightness(int percent) {
        return sendPayload(
                String.format(Locale.US, "{\"brightness\":%d,\"source\":\"screenBrightness\"}", percent)
        );
    }

    private static String sendPayload(String payload) {
        try {
            ensureConnected();
            byte[] jsonBytes = payload.getBytes(StandardCharsets.UTF_8);
            output.writeInt(jsonBytes.length);
            output.write(jsonBytes);
            output.flush();
            int ack = input.read();
            if (ack != 0) {
                return "desktop rejected value";
            }
            lastSuccessAt = System.currentTimeMillis();
            return "sent";
        } catch (Exception ex) {
            closeQuietly();
            return "desktop connection failed: " + ex.getMessage();
        }
    }

    private static void ensureConnected() throws IOException {
        if (socket == null || socket.isClosed()) {
            socket = new Socket();
            socket.setTcpNoDelay(true);
            socket.connect(new InetSocketAddress(HOST, PORT), 1500);
            socket.setSoTimeout(5000);
            output = new DataOutputStream(socket.getOutputStream());
            input = new DataInputStream(socket.getInputStream());
        }
    }

    private static void closeQuietly() {
        try {
            if (output != null) output.close();
        } catch (Exception ignored) {
        }
        try {
            if (input != null) input.close();
        } catch (Exception ignored) {
        }
        try {
            if (socket != null) socket.close();
        } catch (Exception ignored) {
        }
        socket = null;
        output = null;
        input = null;
    }
}
