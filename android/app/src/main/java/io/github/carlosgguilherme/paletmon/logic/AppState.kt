package io.github.carlosgguilherme.paletmon.logic

import io.github.carlosgguilherme.paletmon.data.Dex
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import java.io.File

// Field names match the JSON the previous Android app saved, so an update keeps the user's team.

@Serializable
data class TeamSlot(
    @SerialName("Id") val id: Int,
    @SerialName("Lv") val lv: Int,
    @SerialName("Ab") val ab: String? = null,
)

@Serializable
data class Opponent(
    @SerialName("Mode") val mode: String = "type",
    @SerialName("T1") val t1: String = "water",
    @SerialName("T2") val t2: String = "",
    @SerialName("Mon") val mon: String = "",
)

@Serializable
data class BattleMode(
    @SerialName("Preset") val preset: String = "livre",
    @SerialName("Format") val format: String = "singles",
    @SerialName("Size") val size: Int = 6,
    @SerialName("Pick") val pick: Int = 6,
    @SerialName("Level") val level: String = "free",
    @SerialName("BanLegends") val banLegends: Boolean = false,
    @SerialName("SleepClause") val sleepClause: Boolean = false,
)

// Checkbox configuration shared by the Generator and "Montar" screens.
@Serializable
data class BoxConfig(
    @SerialName("Flags") val flags: Map<String, Boolean> = emptyMap(),
    @SerialName("WeatherKind") val weatherKind: String = "rain",
    @SerialName("MonoType") val monoType: String = "water",
    @SerialName("GenNum") val genNum: Int = 9,
) {
    operator fun get(key: String) = flags[key] == true
    fun with(key: String, value: Boolean) = copy(flags = flags + (key to value))

    companion object {
        fun of(vararg on: String) = BoxConfig(flags = on.associateWith { true })
    }
}

@Serializable
data class AppState(
    @SerialName("Team") val team: List<TeamSlot> = listOf(TeamSlot(906, 11), TeamSlot(554, 25), TeamSlot(228, 29), TeamSlot(935, 25)),
    @SerialName("Wish") val wish: List<Int> = emptyList(),
    @SerialName("Tms") val tms: Boolean = true,
    @SerialName("Finals") val finals: Boolean = true,
    @SerialName("Legends") val legends: Boolean = false,
    @SerialName("Tab") val tab: String = "time",
    @SerialName("Opp") val opp: Opponent = Opponent(),
    @SerialName("Gen") val gen: BoxConfig = BoxConfig.of("synergy", "balanced"),
    @SerialName("Build") val build: BoxConfig = BoxConfig.of("synergy", "balanced"),
    @SerialName("Mode") val mode: BattleMode = BattleMode(),
) {
    companion object {
        private val json = Json { ignoreUnknownKeys = true; encodeDefaults = true; prettyPrint = true }

        fun file(filesDir: File) = File(filesDir, ".config/PaletMon/estado.json")

        fun load(filesDir: File): AppState = try {
            val f = file(filesDir)
            if (!f.exists()) AppState()
            else {
                val s = json.decodeFromString<AppState>(f.readText())
                s.copy(team = s.team.filter { it.id in Dex.byId }.take(6), wish = s.wish.filter { it in Dex.byId })
            }
        } catch (_: Exception) {
            AppState()
        }

        fun save(state: AppState, filesDir: File) {
            try {
                val f = file(filesDir)
                f.parentFile?.mkdirs()
                f.writeText(json.encodeToString(serializer(), state))
            } catch (_: Exception) {
            }
        }
    }
}
