import java.util.Properties

plugins {
    java
}

val sdkDir = run {
    val properties = Properties()
    rootProject.file("local.properties").inputStream().use(properties::load)
    file(properties.getProperty("sdk.dir"))
}

val compileSdk = 35
val minSdk = 26
val androidJar = sdkDir.resolve("platforms/android-$compileSdk/android.jar")
val d8Executable = sdkDir.resolve("build-tools")
    .listFiles()
    .orEmpty()
    .filter { it.isDirectory }
    .maxByOrNull { it.name }
    ?.resolve(if (System.getProperty("os.name").contains("Windows", ignoreCase = true)) "d8.bat" else "d8")
    ?: error("Android SDK build-tools d8 was not found.")

java {
    sourceCompatibility = JavaVersion.VERSION_17
    targetCompatibility = JavaVersion.VERSION_17
}

dependencies {
    compileOnly(files(androidJar))
}

val dexOutputDir = layout.buildDirectory.dir("intermediates/headlessDex")

val javaClassesJar = tasks.register<Jar>("javaClassesJar") {
    dependsOn(tasks.named("classes"))

    archiveFileName.set("deskbrightness-headless-classes.jar")
    destinationDirectory.set(layout.buildDirectory.dir("intermediates/headlessClasses"))

    from(layout.buildDirectory.dir("classes/java/main"))
}

tasks.register<Exec>("dexHeadless") {
    dependsOn(javaClassesJar)

    inputs.file(javaClassesJar.flatMap { it.archiveFile })
    outputs.dir(dexOutputDir)

    doFirst {
        delete(dexOutputDir)
        mkdir(dexOutputDir)
    }

    commandLine(
        d8Executable.absolutePath,
        "--lib",
        androidJar.absolutePath,
        "--min-api",
        minSdk.toString(),
        "--output",
        dexOutputDir.get().asFile.absolutePath,
        javaClassesJar.get().archiveFile.get().asFile.absolutePath
    )
}

tasks.register<Zip>("headlessJar") {
    dependsOn(tasks.named("dexHeadless"))

    archiveFileName.set("deskbrightness-headless.jar")
    destinationDirectory.set(layout.buildDirectory.dir("outputs/headless"))

    from(dexOutputDir) {
        include("classes.dex")
    }
}

tasks.register("buildHeadless") {
    dependsOn(tasks.named("headlessJar"))
}
