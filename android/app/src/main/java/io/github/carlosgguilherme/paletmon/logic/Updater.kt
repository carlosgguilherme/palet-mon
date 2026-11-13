package io.github.carlosgguilherme.paletmon.logic

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.core.content.FileProvider
import io.github.carlosgguilherme.paletmon.BuildConfig
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.long
import java.io.File
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL

class UpdateOffer(val version: String, val url: String, val bytes: Long)

// Reads the latest GitHub release, downloads its APK and hands it to the Android installer.
class Updater(private val context: Context) {
    private val apkFile get() = File(context.cacheDir, "updates/Palet-Mon.apk")

    private fun open(url: String) = (URL(url).openConnection() as HttpURLConnection).apply {
        connectTimeout = 15_000
        readTimeout = 30_000
        setRequestProperty("User-Agent", "PaletMon/${BuildConfig.VERSION_NAME}")
    }

    private fun parts(v: String) = v.split(".").map { it.toIntOrNull() ?: 0 }

    private fun isNewer(latest: String, current: String): Boolean {
        val a = parts(latest)
        val b = parts(current)
        for (i in 0 until maxOf(a.size, b.size)) {
            val x = a.getOrElse(i) { 0 }
            val y = b.getOrElse(i) { 0 }
            if (x != y) return x > y
        }
        return false
    }

    suspend fun check(): UpdateOffer? = withContext(Dispatchers.IO) {
        try {
            fromApi()
        } catch (_: Exception) {
            fromReleasePage()
        }
    }

    private fun fromApi(): UpdateOffer? {
        val text = open(API_URL).inputStream.bufferedReader().use { it.readText() }
        val root = Json.parseToJsonElement(text).jsonObject
        val latest = root.getValue("tag_name").jsonPrimitive.content.removePrefix("v")
        if (!isNewer(latest, BuildConfig.VERSION_NAME)) return null
        val asset = root.getValue("assets").jsonArray.map { it.jsonObject }
            .firstOrNull { it.getValue("name").jsonPrimitive.content == APK_NAME } ?: return null
        return UpdateOffer(latest, asset.getValue("browser_download_url").jsonPrimitive.content, asset.getValue("size").jsonPrimitive.long)
    }

    // The API answers only 60 requests an hour per internet address, which a shared mobile address can use up.
    // The release page has no such limit: it redirects to the latest tag.
    private fun fromReleasePage(): UpdateOffer? {
        val page = open("$REPO_URL/releases/latest").apply { instanceFollowRedirects = false }
        val tag = page.getHeaderField("Location")?.substringAfter("/releases/tag/", "").orEmpty()
        page.disconnect()
        if (tag.isEmpty()) throw IOException("versão não encontrada")
        val latest = tag.removePrefix("v")
        if (!isNewer(latest, BuildConfig.VERSION_NAME)) return null
        val url = "$REPO_URL/releases/download/$tag/$APK_NAME"
        val head = open(url).apply { requestMethod = "HEAD" }
        val found = head.responseCode == HttpURLConnection.HTTP_OK
        val bytes = head.contentLengthLong
        head.disconnect()
        // the APK is attached a few minutes after the release; until then there is nothing to offer
        return if (found) UpdateOffer(latest, url, maxOf(0L, bytes)) else null
    }

    suspend fun download(offer: UpdateOffer, progress: (Int) -> Unit) = withContext(Dispatchers.IO) {
        apkFile.parentFile?.mkdirs()
        val conn = open(offer.url)
        val total = conn.contentLengthLong.takeIf { it > 0 } ?: offer.bytes
        conn.inputStream.use { input ->
            apkFile.outputStream().use { output ->
                val buffer = ByteArray(64 * 1024)
                var done = 0L
                while (true) {
                    val read = input.read(buffer)
                    if (read < 0) break
                    output.write(buffer, 0, read)
                    done += read
                    if (total > 0) progress((done * 100 / total).toInt())
                }
            }
        }
    }

    // Returns what the user still has to do.
    fun install(): String {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && !context.packageManager.canRequestPackageInstalls()) {
            val settings = Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:${context.packageName}"))
            settings.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            context.startActivity(settings)
            return "Permita que o Palet Mon instale atualizações, volte aqui e toque em Instalar."
        }
        val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", apkFile)
        val install = Intent(Intent.ACTION_VIEW)
        install.setDataAndType(uri, "application/vnd.android.package-archive")
        install.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK)
        context.startActivity(install)
        return "Confirme a instalação na tela do Android. Se ela não aparecer, toque em Instalar."
    }

    companion object {
        const val REPO_URL = "https://github.com/carlosgguilherme/palet-mon"
        private const val API_URL = "https://api.github.com/repos/carlosgguilherme/palet-mon/releases/latest"
        private const val APK_NAME = "Palet-Mon-Android.apk"
    }
}
