package com.deskbrightness.mobile;

enum BridgeMode {
    LIGHT_SENSOR("lightSensor"),
    SCREEN_BRIGHTNESS("screenBrightness");

    private final String wireName;

    BridgeMode(String wireName) {
        this.wireName = wireName;
    }

    public String wireName() {
        return wireName;
    }

    public static BridgeMode fromArgument(String value) {
        if (value == null) {
            return LIGHT_SENSOR;
        }

        for (BridgeMode mode : values()) {
            if (mode.wireName.equals(value)) {
                return mode;
            }
        }

        return LIGHT_SENSOR;
    }
}
