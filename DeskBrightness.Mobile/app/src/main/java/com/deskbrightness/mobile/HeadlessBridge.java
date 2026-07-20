package com.deskbrightness.mobile;

import android.content.Context;
import android.database.ContentObserver;
import android.hardware.Sensor;
import android.hardware.SensorEvent;
import android.hardware.SensorEventListener;
import android.hardware.SensorManager;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;

import java.io.FileWriter;
import java.io.PrintWriter;
import java.io.StringWriter;
import java.lang.reflect.Method;
import java.util.Locale;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

public final class HeadlessBridge {
    private static final String PACKAGE_NAME = "com.deskbrightness.mobile";
    private static final String INTERNAL_LOG_PATH = "/data/local/tmp/deskbrightness-headless-internal.log";
    private static final long SENSOR_PUBLISH_INTERVAL_MS = 500L;
    private static final long SENSOR_WAIT_LOG_INTERVAL_MS = 5000L;
    private static final float MINIMUM_LUX_DELTA = 1f;
    private static final long DISCONNECT_TIMEOUT_MS = 30_000L;

    private final Context context;
    private final BridgeMode mode;
    private volatile float latestLux = -1f;
    private volatile long firstDisconnectAt = 0L;

    private HeadlessBridge(Context context, BridgeMode mode) {
        this.context = context;
        this.mode = mode;
    }

    public static void main(String[] args) throws Throwable {
        Thread.setDefaultUncaughtExceptionHandler((thread, throwable) ->
                logThrowable("uncaught on " + thread.getName(), throwable)
        );

        try {
            run(args);
        } catch (Throwable throwable) {
            logThrowable("fatal main failure", throwable);
            throw throwable;
        }
    }

    private static void run(String[] args) throws Exception {
        BridgeMode mode = parseMode(args);

        log("main entered args=" + String.join(" ", args));

        if (hasArgument(args, "--probe-basic")) {
            log("probe basic ok");
            return;
        }

        if (Looper.myLooper() == null) {
            Looper.prepare();
        }

        log("looper ready");

        Context context = createAppContext();

        log("context ready: " + context.getPackageName());

        if (hasArgument(args, "--probe")) {
            runProbe(context);
            return;
        }

        log("starting headless bridge: " + mode.wireName() + " / package=" + context.getPackageName());

        new HeadlessBridge(context, mode).start();
        log("entering looper");
        Looper.loop();
    }

    private void start() {
        if (mode == BridgeMode.SCREEN_BRIGHTNESS) {
            startScreenBrightnessMode();
        } else {
            startLightSensorMode();
        }
    }

    private void checkConnectionOrExit(String result) {
        if (result.equals("sent")) {
            firstDisconnectAt = 0L;
            return;
        }

        long now = System.currentTimeMillis();

        if (firstDisconnectAt == 0L) {
            firstDisconnectAt = now;
            log("desktop disconnected, waiting " + (DISCONNECT_TIMEOUT_MS / 1000) + "s before exit");
            return;
        }

        if (now - firstDisconnectAt >= DISCONNECT_TIMEOUT_MS) {
            log("desktop unreachable for " + (DISCONNECT_TIMEOUT_MS / 1000) + "s, exiting");
            System.exit(0);
        }
    }

    private void startLightSensorMode() {
        SensorManager sensorManager = context.getSystemService(SensorManager.class);

        if (sensorManager == null) {
            log("sensor manager not available");
            return;
        }

        Sensor lightSensor = sensorManager.getDefaultSensor(Sensor.TYPE_LIGHT);

        if (lightSensor == null) {
            log("light sensor not found");
            return;
        }

        log("light sensor: " + lightSensor.getName()
                + " / " + lightSensor.getVendor()
                + " / maxRange=" + lightSensor.getMaximumRange() + " lux");

        SensorEventListener listener = new SensorEventListener() {
            @Override
            public void onSensorChanged(SensorEvent event) {
                if (event.values.length > 0) {
                    latestLux = event.values[0];
                }
            }

            @Override
            public void onAccuracyChanged(Sensor sensor, int accuracy) {
            }
        };

        boolean registered = sensorManager.registerListener(
                listener,
                lightSensor,
                SensorManager.SENSOR_DELAY_NORMAL,
                new Handler(Looper.myLooper())
        );

        log("light sensor listener registered: " + registered);

        if (!registered) {
            return;
        }

        ScheduledExecutorService executor = Executors.newSingleThreadScheduledExecutor();
        final float[] lastSentLux = new float[]{Float.NaN};
        final long[] lastWaitLogAt = new long[]{0L};

        executor.scheduleWithFixedDelay(() -> {
            float lux = latestLux;

            if (lux < 0f) {
                long now = System.currentTimeMillis();

                if (now - lastWaitLogAt[0] >= SENSOR_WAIT_LOG_INTERVAL_MS) {
                    lastWaitLogAt[0] = now;
                    log("waiting for first light sensor event");
                }

                return;
            }

            if (!Float.isNaN(lastSentLux[0]) && Math.abs(lux - lastSentLux[0]) < MINIMUM_LUX_DELTA) {
                return;
            }

            String result = DesktopClient.sendLux(lux);
            lastSentLux[0] = lux;
            log(String.format(Locale.US, "light %.1f lux: %s", lux, result));
            checkConnectionOrExit(result);
        }, 0, SENSOR_PUBLISH_INTERVAL_MS, TimeUnit.MILLISECONDS);

        log("light sensor publisher scheduled");
    }

    private void startScreenBrightnessMode() {
        Handler handler = new Handler(Looper.myLooper());

        Runnable publish = () -> {
            int percent = readScreenBrightnessPercent();
            String result = DesktopClient.sendBrightness(percent);
            log("screen brightness " + percent + "%: " + result);
            checkConnectionOrExit(result);
        };

        ContentObserver observer = new ContentObserver(handler) {
            @Override
            public void onChange(boolean selfChange) {
                publish.run();
            }
        };

        context.getContentResolver().registerContentObserver(
                Settings.System.getUriFor(Settings.System.SCREEN_BRIGHTNESS),
                false,
                observer
        );

        publish.run();
    }

    private int readScreenBrightnessPercent() {
        int raw = Settings.System.getInt(
                context.getContentResolver(),
                Settings.System.SCREEN_BRIGHTNESS,
                0
        );

        int clamped = Math.max(0, Math.min(255, raw));
        return Math.max(0, Math.min(100, Math.round((clamped * 100f) / 255f)));
    }

    private static BridgeMode parseMode(String[] args) {
        for (int i = 0; i < args.length - 1; i++) {
            if ("--mode".equals(args[i])) {
                return BridgeMode.fromArgument(args[i + 1]);
            }
        }

        return BridgeMode.LIGHT_SENSOR;
    }

    private static boolean hasArgument(String[] args, String argument) {
        for (String value : args) {
            if (argument.equals(value)) {
                return true;
            }
        }

        return false;
    }

    private static void runProbe(Context context) {
        log("probe package=" + context.getPackageName());

        SensorManager sensorManager = context.getSystemService(SensorManager.class);

        if (sensorManager == null) {
            log("probe sensor manager not available");
            return;
        }

        Sensor lightSensor = sensorManager.getDefaultSensor(Sensor.TYPE_LIGHT);

        if (lightSensor == null) {
            log("probe light sensor not found");
            return;
        }

        log("probe light sensor=" + lightSensor.getName()
                + " / " + lightSensor.getVendor()
                + " / maxRange=" + lightSensor.getMaximumRange() + " lux");
    }

    private static Context createAppContext() throws Exception {
        Context systemContext = createSystemContext();

        try {
            return systemContext.createPackageContext(
                    PACKAGE_NAME,
                    Context.CONTEXT_INCLUDE_CODE | Context.CONTEXT_IGNORE_SECURITY
            );
        } catch (Exception ex) {
            log("package context failed, falling back to system context: " + ex.getMessage());
            return systemContext;
        }
    }

    private static Context createSystemContext() throws Exception {
        Class<?> activityThreadClass = Class.forName("android.app.ActivityThread");
        Method systemMain = activityThreadClass.getDeclaredMethod("systemMain");
        Object activityThread = systemMain.invoke(null);
        Method getSystemContext = activityThreadClass.getDeclaredMethod("getSystemContext");
        return (Context) getSystemContext.invoke(activityThread);
    }

    private static void log(String message) {
        String line = "DeskBrightnessHeadless: " + message;
        System.out.println(line);
        appendInternalLog(line);
    }

    private static void logThrowable(String label, Throwable throwable) {
        StringWriter writer = new StringWriter();
        throwable.printStackTrace(new PrintWriter(writer));
        log(label + ": " + throwable.getClass().getName() + ": " + throwable.getMessage());

        for (String line : writer.toString().split("\\r?\\n")) {
            if (!line.isEmpty()) {
                appendInternalLog("DeskBrightnessHeadless: " + line);
            }
        }
    }

    private static synchronized void appendInternalLog(String line) {
        try (FileWriter writer = new FileWriter(INTERNAL_LOG_PATH, true)) {
            writer.write(line);
            writer.write('\n');
        } catch (Exception ignored) {
        }
    }
}
