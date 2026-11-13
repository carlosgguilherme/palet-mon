plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
    id("org.jetbrains.kotlin.plugin.serialization")
}

// Releases pass -PappVersion from the tag.
val appVersion = (findProperty("appVersion") as String?)?.removePrefix("v") ?: "2.3.1"
val versionParts = appVersion.split(".").map { it.toInt() }
val dexDir = layout.buildDirectory.dir("generated/dex")

android {
    namespace = "io.github.carlosgguilherme.paletmon"
    compileSdk = 35

    defaultConfig {
        applicationId = "io.github.carlosgguilherme.paletmon"
        minSdk = 24
        targetSdk = 35
        versionCode = versionParts[0] * 10000 + versionParts[1] * 100 + versionParts[2]
        versionName = appVersion
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            // emulator tests only: real releases are signed on the PC by publicar-android.ps1
            if (findProperty("signWithDebug") == "true") signingConfig = signingConfigs.getByName("debug")
        }
    }

    sourceSets["main"].assets.srcDir(dexDir)
    sourceSets["test"].resources.srcDir(dexDir)
}

// dex.json has a single home, shared with the desktop app.
val copyDex by tasks.registering(Copy::class) {
    from("../../PaletMon.Core/Assets/dex.json")
    into(dexDir)
}
tasks.named("preBuild") { dependsOn(copyDex) }

dependencies {
    val composeBom = platform("androidx.compose:compose-bom:2024.12.01")
    implementation(composeBom)
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.foundation:foundation")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.activity:activity-compose:1.9.3")
    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.8.7")
    implementation("androidx.profileinstaller:profileinstaller:1.4.1")
    implementation("io.coil-kt:coil-compose:2.7.0")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.9.0")
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.7.3")
    testImplementation("junit:junit:4.13.2")
}
