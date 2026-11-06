package io.github.carlosgguilherme.paletmon.logic

import java.math.BigDecimal
import java.math.RoundingMode

object TypeChart {
    val all = listOf(
        "normal", "fighting", "flying", "poison", "ground", "rock", "bug", "ghost", "steel",
        "fire", "water", "grass", "electric", "psychic", "ice", "dragon", "dark", "fairy",
    )

    val pt = mapOf(
        "normal" to "Normal", "fighting" to "Lutador", "flying" to "Voador", "poison" to "Venenoso", "ground" to "Terra", "rock" to "Pedra",
        "bug" to "Inseto", "ghost" to "Fantasma", "steel" to "Aço", "fire" to "Fogo", "water" to "Água", "grass" to "Planta",
        "electric" to "Elétrico", "psychic" to "Psíquico", "ice" to "Gelo", "dragon" to "Dragão", "dark" to "Sombrio", "fairy" to "Fada",
    )

    val color = mapOf(
        "normal" to 0xFF9FA19F, "fighting" to 0xFFE07000, "flying" to 0xFF6FA8E0, "poison" to 0xFF9141CB, "ground" to 0xFF915121, "rock" to 0xFFA39C70,
        "bug" to 0xFF859414, "ghost" to 0xFF704170, "steel" to 0xFF5590A6, "fire" to 0xFFE62829, "water" to 0xFF2980EF, "grass" to 0xFF3FA129,
        "electric" to 0xFFF2B600, "psychic" to 0xFFEF4179, "ice" to 0xFF3DC4E8, "dragon" to 0xFF5060E1, "dark" to 0xFF624D4E, "fairy" to 0xFFE46AE4,
    )

    val darkText = setOf("electric", "ice", "normal", "rock")

    private class Row(val se: List<String>, val nve: List<String>, val im: List<String> = emptyList())

    private val chart = mapOf(
        "normal" to Row(emptyList(), listOf("rock", "steel"), listOf("ghost")),
        "fighting" to Row(listOf("normal", "rock", "steel", "ice", "dark"), listOf("flying", "poison", "bug", "psychic", "fairy"), listOf("ghost")),
        "flying" to Row(listOf("fighting", "bug", "grass"), listOf("rock", "steel", "electric")),
        "poison" to Row(listOf("grass", "fairy"), listOf("poison", "ground", "rock", "ghost"), listOf("steel")),
        "ground" to Row(listOf("poison", "rock", "steel", "fire", "electric"), listOf("bug", "grass"), listOf("flying")),
        "rock" to Row(listOf("flying", "bug", "fire", "ice"), listOf("fighting", "ground", "steel")),
        "bug" to Row(listOf("grass", "psychic", "dark"), listOf("fighting", "flying", "poison", "ghost", "steel", "fire", "fairy")),
        "ghost" to Row(listOf("ghost", "psychic"), listOf("dark"), listOf("normal")),
        "steel" to Row(listOf("rock", "ice", "fairy"), listOf("steel", "fire", "water", "electric")),
        "fire" to Row(listOf("bug", "steel", "grass", "ice"), listOf("rock", "fire", "water", "dragon")),
        "water" to Row(listOf("ground", "rock", "fire"), listOf("water", "grass", "dragon")),
        "grass" to Row(listOf("ground", "rock", "water"), listOf("flying", "poison", "bug", "steel", "fire", "grass", "dragon")),
        "electric" to Row(listOf("flying", "water"), listOf("grass", "electric", "dragon"), listOf("ground")),
        "psychic" to Row(listOf("fighting", "poison"), listOf("steel", "psychic"), listOf("dark")),
        "ice" to Row(listOf("flying", "ground", "grass", "dragon"), listOf("steel", "fire", "water", "ice")),
        "dragon" to Row(listOf("dragon"), listOf("steel"), listOf("fairy")),
        "dark" to Row(listOf("ghost", "psychic"), listOf("fighting", "dark", "fairy")),
        "fairy" to Row(listOf("fighting", "dragon", "dark"), listOf("poison", "steel", "fire")),
    )

    private val table = Array(18) { a ->
        DoubleArray(18) { d ->
            val row = chart.getValue(all[a])
            val def = all[d]
            when (def) {
                in row.im -> 0.0
                in row.se -> 2.0
                in row.nve -> 0.5
                else -> 1.0
            }
        }
    }

    fun index(type: String) = all.indexOf(type)
    fun eff(atk: String, def: String) = table[index(atk)][index(def)]

    val abilityDef = mapOf(
        "Levitate" to mapOf("ground" to 0.0), "Earth Eater" to mapOf("ground" to 0.0), "Flash Fire" to mapOf("fire" to 0.0),
        "Well Baked Body" to mapOf("fire" to 0.0), "Water Absorb" to mapOf("water" to 0.0), "Storm Drain" to mapOf("water" to 0.0),
        "Dry Skin" to mapOf("water" to 0.0, "fire" to 1.25), "Volt Absorb" to mapOf("electric" to 0.0), "Lightning Rod" to mapOf("electric" to 0.0),
        "Motor Drive" to mapOf("electric" to 0.0), "Sap Sipper" to mapOf("grass" to 0.0), "Thick Fat" to mapOf("fire" to 0.5, "ice" to 0.5),
        "Heatproof" to mapOf("fire" to 0.5), "Water Bubble" to mapOf("fire" to 0.5), "Purifying Salt" to mapOf("ghost" to 0.5),
        "Fluffy" to mapOf("fire" to 2.0),
    )

    fun mult(m: Double): String = when (m) {
        0.0 -> "0"
        0.25 -> "¼"
        0.5 -> "½"
        1.0 -> "1"
        1.25 -> "1¼"
        2.0 -> "2"
        4.0 -> "4"
        else -> BigDecimal(m).setScale(2, RoundingMode.HALF_UP).stripTrailingZeros().toPlainString()
    }
}
