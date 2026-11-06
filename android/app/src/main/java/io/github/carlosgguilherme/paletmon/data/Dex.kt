package io.github.carlosgguilherme.paletmon.data

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.int
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive

class Evo(val level: Int, val item: String?, val how: String?, val time: String?)

class Mon(
    val id: Int,
    val species: Int,
    val name: String,
    val types: List<String>,
    val stats: IntArray, // hp, atk, def, spa, spd, spe
    val abilities: List<String>, // hidden ones start with '*'
    val from: Int, // species it evolves from, 0 if none
    val chain: Int,
    val gen: Int,
    val legendary: Boolean,
    val mythical: Boolean,
    val regional: Boolean,
    val evo: Evo?,
    val levelMoves: IntArray, // pairs: move id, level
    val otherMoves: IntArray, // TM, tutor, egg
) {
    val bst = stats.sum()
}

class MoveInfo(
    val id: Int,
    val name: String,
    val type: String,
    val power: Int, // -1 variable, 0 status
    val accuracy: Int, // 101 = never misses
    val cls: Int, // 1 status, 2 physical, 3 special
    val priority: Int,
)

object Dex {
    private val typeIds = listOf(
        "", "normal", "fighting", "flying", "poison", "ground", "rock", "bug", "ghost", "steel",
        "fire", "water", "grass", "electric", "psychic", "ice", "dragon", "dark", "fairy",
    )

    var all: List<Mon> = emptyList()
        private set
    var byId: Map<Int, Mon> = emptyMap()
        private set
    var moves: Map<Int, MoveInfo> = emptyMap()
        private set
    private var byName: Map<String, Mon> = emptyMap()

    private fun JsonArray.ints() = IntArray(size) { this[it].jsonPrimitive.int }
    private fun JsonObject.text(key: String) = this[key]?.jsonPrimitive?.content

    fun load(text: String) {
        val root = Json.parseToJsonElement(text).jsonObject

        moves = root.getValue("moves").jsonObject.entries.associate { (key, value) ->
            val a = value.jsonArray
            val id = key.toInt()
            id to MoveInfo(
                id, a[0].jsonPrimitive.content, typeIds[a[1].jsonPrimitive.int], a[2].jsonPrimitive.int,
                a[3].jsonPrimitive.int, a[4].jsonPrimitive.int, a[5].jsonPrimitive.int,
            )
        }

        all = root.getValue("pokemon").jsonArray.map { element ->
            val p = element.jsonObject
            val evo = p["e"]?.jsonObject?.let { e ->
                Evo(e["lv"]?.jsonPrimitive?.int ?: 0, e.text("item"), e.text("how"), e.text("time"))
            }
            val learn = p["m"]?.jsonObject
            Mon(
                id = p.getValue("i").jsonPrimitive.int,
                species = p.getValue("s").jsonPrimitive.int,
                name = p.getValue("n").jsonPrimitive.content,
                types = p.getValue("t").jsonArray.map { typeIds[it.jsonPrimitive.int] },
                stats = p.getValue("b").jsonArray.ints(),
                abilities = p.getValue("a").jsonArray.map { it.jsonPrimitive.content },
                from = p.getValue("f").jsonPrimitive.int,
                chain = p.getValue("c").jsonPrimitive.int,
                gen = p.getValue("g").jsonPrimitive.int,
                legendary = "L" in p,
                mythical = "M" in p,
                regional = "r" in p,
                evo = evo,
                levelMoves = learn?.getValue("l")?.jsonArray?.ints() ?: IntArray(0),
                otherMoves = learn?.getValue("o")?.jsonArray?.ints() ?: IntArray(0),
            )
        }
        byId = all.associateBy { it.id }
        val names = HashMap<String, Mon>(all.size * 2)
        for (m in all) names.putIfAbsent(m.name.lowercase(), m)
        byName = names
    }

    fun find(name: String?): Mon? = name?.let { byName[it.trim().lowercase()] }
}
