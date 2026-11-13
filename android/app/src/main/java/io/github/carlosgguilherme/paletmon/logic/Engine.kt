package io.github.carlosgguilherme.paletmon.logic

import io.github.carlosgguilherme.paletmon.data.Dex
import io.github.carlosgguilherme.paletmon.data.Evo
import io.github.carlosgguilherme.paletmon.data.Mon
import io.github.carlosgguilherme.paletmon.data.MoveInfo
import io.github.carlosgguilherme.paletmon.logic.TypeChart.all
import io.github.carlosgguilherme.paletmon.logic.TypeChart.eff
import io.github.carlosgguilherme.paletmon.logic.TypeChart.pt
import java.util.concurrent.ConcurrentHashMap
import kotlin.math.max
import kotlin.math.min
import kotlin.random.Random

// Same rules as the desktop engine (PaletMon.Core/Logic/Engine.cs); ParityTest compares the two.
// Text fields use **bold** markup, rendered by the UI.

class Member(
    val p: Mon,
    val ab: String,
    val lv: Int = 0,
    val index: Int = -1,
    val core: Boolean = false,
    val kept: Boolean = false,
) {
    var moves: List<PMove> = emptyList()
    val dv: DoubleArray by lazy { DoubleArray(18) { Engine.defMult(p, ab, all[it]) } }
}

class PMove(val info: MoveInfo, val src: String) {
    var value = 0.0
    val name get() = info.name
    val type get() = info.type
}

class DefRow(val atk: String, val mults: DoubleArray, val weak: Int, val res: Int)

class TeamScore(
    val score: Double,
    val defPen: Double,
    val dupPen: Double,
    val avgBst: Double,
    val covered: List<String>,
    val holes: List<String>,
    val table: List<DefRow>,
    val typeCount: Map<String, Int>,
)

class GenResult(val team: List<Member>, val score: Double, val poolSize: Int, val key: String, val sc: TeamScore)

data class Alert(val kind: String, val text: String)
data class Role(val name: String, val tip: String)
data class Feat(val spread: Int, val fake: Boolean, val redirect: Boolean, val speed: Boolean, val eq: Boolean)
data class BoxDef(val key: String, val title: String, val desc: String, val extra: String? = null)
data class Preset(val label: String, val format: String, val size: Int, val pick: Int, val level: String, val banLegends: Boolean, val sleepClause: Boolean)
data class Weather(val label: String, val setters: List<String>, val abusers: List<String>, val good: List<String>, val bad: List<String>)
class Ranked(val x: Member, val best: PMove?, val mult: Double, val incoming: Double, val speed: String?, val score: Double)
class EvBuild(val nature: String, val plus: Int, val minus: Int, val evs: IntArray, val target: Mon)
class Coverage(val type: String, val best: Double, val who: List<String>)
class Suggestion(val p: Mon, val why: String)

object Engine {
    @Volatile var s = AppState()
    private val rng = Random.Default

    // ---------- constants ----------
    private val setupP = listOf("Swords Dance", "Dragon Dance", "Bulk Up", "Victory Dance", "Shell Smash", "Belly Drum", "Hone Claws", "Shift Gear", "Coil")
    private val setupS = listOf("Nasty Plot", "Quiver Dance", "Calm Mind", "Shell Smash", "Work Up", "Growth", "Tidy Up")
    private val statDrop = setOf("Overheat", "Draco Meteor", "Leaf Storm", "Psycho Boost", "Fleur Cannon", "Make It Rain", "Superpower", "Close Combat", "Hammer Arm", "Ice Hammer", "V Create", "Headlong Rush", "Armor Cannon", "Outrage", "Petal Dance", "Thrash", "Raging Fury")
    private val sleepMoves = setOf("Spore", "Sleep Powder", "Hypnosis", "Yawn", "Sing", "Lovely Kiss", "Dark Void", "Grass Whistle")
    private val spread = setOf(
        "Earthquake", "Rock Slide", "Heat Wave", "Dazzling Gleam", "Hyper Voice", "Surf", "Blizzard", "Discharge", "Muddy Water",
        "Icy Wind", "Snarl", "Eruption", "Water Spout", "Make It Rain", "Bleakwind Storm", "Sandsear Storm", "Wildbolt Storm", "Springtide Storm",
        "Electroweb", "Lava Plume", "Sludge Wave", "Boomburst", "Bulldoze", "Breaking Swipe", "Glacial Lance", "Astral Barrage", "Diamond Storm",
        "Petal Blizzard", "Parabolic Charge", "Mortal Spin", "Air Cutter", "Struggle Bug", "Precipice Blades", "Origin Pulse", "Matcha Gotcha",
    )
    private val redirect = listOf("Follow Me", "Rage Powder")
    private val speedCtrl = listOf("Tailwind", "Trick Room", "Icy Wind", "Electroweb", "Thunder Wave")

    val presets = linkedMapOf(
        "livre" to Preset("Livre · Singles 6x6", "singles", 6, 6, "free", false, false),
        "comp" to Preset("Singles competitivo 6x6 · Nv 100", "singles", 6, 6, "100", true, true),
        "bss" to Preset("Singles 3 de 6 · Nv 50", "singles", 6, 3, "50", true, false),
        "x3" to Preset("Singles 3x3", "singles", 3, 3, "free", false, false),
        "x1" to Preset("X1 · 1 contra 1", "singles", 1, 1, "free", false, false),
        "vgc" to Preset("Duplas 4 de 6 · Nv 50 (estilo VGC)", "doubles", 6, 4, "50", true, false),
        "dbl6" to Preset("Duplas 6x6", "doubles", 6, 6, "free", false, false),
        "tpl6" to Preset("Triplas 6x6", "triples", 6, 6, "free", false, false),
        "lc" to Preset("Little Cup · Nv 5, só 1º estágio", "singles", 6, 6, "lc", true, true),
    )
    val formatLabel = linkedMapOf("singles" to "Singles", "doubles" to "Duplas", "triples" to "Triplas")
    val levelLabel = linkedMapOf("free" to "Nível do jogo", "50" to "Nivelado no 50", "100" to "Nivelado no 100", "lc" to "Little Cup (Nv 5)")
    val formatTips = mapOf(
        "singles" to listOf(
            "Um Pokémon de cada lado. Trocar na hora certa vale mais que qualquer golpe.",
            "Golpes de setup (Swords Dance, Nasty Plot) rendem muito quando o adversário em campo não ameaça você.",
            "Tenha uma resposta para cada tipo que acerta seu time em cheio.",
        ),
        "doubles" to listOf(
            "Golpes que acertam os dois adversários (Earthquake, Rock Slide, Heat Wave, Dazzling Gleam) causam 75% em cada alvo e valem muito.",
            "Protect em quase todo mundo: um se protege enquanto o aliado resolve.",
            "Fake Out faz o alvo perder a vez no primeiro turno em campo.",
            "Tailwind e Trick Room decidem quem age primeiro por alguns turnos.",
            "Intimidate baixa o Ataque dos dois adversários ao entrar.",
            "Earthquake e Surf também acertam seu aliado. Combine com parceiros voadores, com Levitate ou que resistam.",
        ),
        "triples" to listOf(
            "Três de cada lado. O Pokémon do meio alcança todos; os das pontas só alcançam o lado mais perto com golpes de alvo único.",
            "Golpes em área valem ainda mais do que em duplas.",
            "Deixe o suporte (Follow Me, Wide Guard, Helping Hand) no meio e os atacantes nas pontas.",
        ),
    )

    val weathers = linkedMapOf(
        "rain" to Weather("Chuva", listOf("Drizzle"), listOf("Swift Swim", "Rain Dish", "Hydration", "Dry Skin"), listOf("water", "electric"), listOf("fire")),
        "sun" to Weather("Sol", listOf("Drought"), listOf("Chlorophyll", "Solar Power", "Flower Gift", "Leaf Guard", "Protosynthesis", "Harvest"), listOf("fire", "grass"), listOf("water")),
        "sand" to Weather("Areia", listOf("Sand Stream", "Sand Spit"), listOf("Sand Rush", "Sand Force", "Sand Veil"), listOf("rock", "ground", "steel"), emptyList()),
        "snow" to Weather("Neve", listOf("Snow Warning"), listOf("Slush Rush", "Ice Body", "Snow Cloak", "Ice Face"), listOf("ice"), emptyList()),
    )

    private val meta = setOf(
        "Great Tusk", "Iron Valiant", "Roaring Moon", "Kingambit", "Gholdengo", "Dragapult", "Garchomp", "Corviknight", "Toxapex", "Ferrothorn",
        "Tyranitar", "Volcarona", "Dragonite", "Gliscor", "Clefable", "Skeledirge", "Excadrill", "Scizor", "Weavile", "Gengar", "Azumarill", "Primarina", "Slowbro",
        "Slowking", "Hatterene", "Grimmsnarl", "Cinderace", "Rillaboom", "Pelipper", "Barraskewda", "Hippowdon", "Mamoswine", "Lucario", "Metagross", "Salamence",
        "Hydreigon", "Haxorus", "Baxcalibur", "Annihilape", "Meowscarada", "Quaquaval", "Garganacl", "Amoonguss", "Blissey", "Tinkaton", "Ceruledge", "Armarouge",
        "Palafin", "Kingdra", "Magnezone", "Breloom", "Conkeldurr", "Hawlucha", "Talonflame", "Torkoal", "Ninetales", "Venusaur", "Charizard", "Swampert", "Mimikyu",
        "Toxtricity", "Dondozo", "Glimmora", "Cyclizar", "Espathra", "Clodsire", "Iron Moth", "Iron Treads", "Iron Hands", "Flutter Mane", "Sandy Shocks",
        "Scream Tail", "Brute Bonnet", "Slither Wing", "Iron Bundle", "Alomomola", "Lokix", "Sneasler", "Ursaluna", "Kleavor", "Basculegion", "Polteageist",
        "Gyarados", "Archaludon", "Hydrapple", "Gouging Fire", "Raging Bolt", "Iron Crown", "Iron Boulder", "Sinistcha", "Heatran", "Landorus", "Zapdos",
        "Latios", "Ting Lu", "Chien Pao", "Ogerpon", "Ninetales Alola", "Arcanine Hisui", "Samurott Hisui", "Goodra Hisui", "Zoroark Hisui", "Weezing Galar", "Slowking Galar",
    )

    val boxes = listOf(
        BoxDef("random", "Aleatório", "Cada clique sorteia um time diferente. Sozinho, gera um time 100% aleatório."),
        BoxDef("synergy", "Sinergia", "Um cobre a fraqueza do outro e o time acerta o máximo de tipos."),
        BoxDef("competitive", "Competitivo", "Pokémon fortes do cenário competitivo, atributos altos e papéis claros."),
        BoxDef("balanced", "Equilibrado", "Mistura atacantes físicos, especiais e pelo menos um tanque."),
        BoxDef("fast", "Rápido", "Prioriza Pokémon com velocidade alta para atacar primeiro."),
        BoxDef("trickroom", "Trick Room", "Pokémon lentos e fortes, com alguém que aprende Trick Room."),
        BoxDef("weather", "Time de clima", "Um Pokémon que liga o clima e outros que se beneficiam dele.", "weatherKind"),
        BoxDef("mono", "Monotipo", "Todos os Pokémon compartilham um tipo.", "monoType"),
        BoxDef("gen", "Uma geração", "Só Pokémon de uma geração.", "genNum"),
        BoxDef("noDupTypes", "Sem tipos repetidos", "Nenhum tipo aparece duas vezes."),
        BoxDef("keepTeam", "Manter meu time atual", "Os Pokémon do seu time ficam e o gerador completa o resto."),
        BoxDef("wishPrio", "Priorizar minha lista", "Dá preferência aos Pokémon da aba Montar."),
        BoxDef("wishOnly", "Só da minha lista", "Usa apenas os Pokémon da sua lista (nas formas finais)."),
        BoxDef("legends", "Permitir lendários", "Inclui lendários e míticos."),
    )
    val buildKeys = listOf("synergy", "competitive", "balanced", "random", "fast", "trickroom", "weather", "mono", "gen", "noDupTypes", "legends")

    // ---------- evolution ----------
    private val evoCache = ConcurrentHashMap<Int, List<Mon>>()
    private val byFrom by lazy { Dex.all.groupBy { it.from } }

    private fun regionOf(p: Mon): String? {
        if (!p.regional) return null
        return listOf("Alola", "Galar", "Hisui", "Paldea").firstOrNull { it in p.name }
    }

    fun nextEvos(p: Mon): List<Mon> = evoCache.getOrPut(p.id) {
        val kids = (byFrom[p.species] ?: emptyList()).filter { it.species != p.species }
        val reg = regionOf(p)
        if (reg != null) {
            val same = kids.filter { regionOf(it) == reg }
            same.ifEmpty { kids.filter { !it.regional } }
        } else kids.filter { !it.regional }
    }

    fun finalForms(p: Mon, seen: MutableSet<Int> = HashSet()): List<Mon> {
        if (!seen.add(p.id)) return listOf(p)
        val n = nextEvos(p)
        return if (n.isEmpty()) listOf(p) else n.flatMap { finalForms(it, seen) }
    }

    fun isFinal(p: Mon) = nextEvos(p).isEmpty()
    fun isBasic(p: Mon) = p.from == 0 && nextEvos(p).isNotEmpty()

    fun evoText(e: Evo?): String {
        if (e == null) return ""
        if (e.level > 0) return "nível ${e.level}" + when {
            e.time == "night" -> " (à noite)"
            e.time == "day" -> " (de dia)"
            e.time != null -> " (${e.time})"
            else -> ""
        }
        if (e.item != null) return "usando ${e.item}"
        return e.how ?: ""
    }

    // ---------- members ----------
    fun abilityNames(p: Mon) = p.abilities.map { it.trimStart('*') }

    fun defaultAbility(p: Mon): String {
        val regular = p.abilities.firstOrNull { !it.startsWith("*") }
        return (regular ?: p.abilities.firstOrNull() ?: "").trimStart('*')
    }

    fun defMult(p: Mon, ability: String?, atk: String): Double {
        var m = 1.0
        for (t in p.types) m *= eff(atk, t)
        val v = TypeChart.abilityDef[ability]?.get(atk)
        if (v != null) m = if (v == 0.0) 0.0 else m * v
        return m
    }

    fun resolveTeam(): List<Member> = s.team.mapIndexedNotNull { i, slot ->
        val p = Dex.byId[slot.id] ?: return@mapIndexedNotNull null
        val ab = if (slot.ab != null && slot.ab in abilityNames(p)) slot.ab else defaultAbility(p)
        Member(p, ab, slot.lv, i)
    }

    fun styleOf(p: Mon): Char {
        val atk = p.stats[1]
        val spa = p.stats[3]
        if (atk >= spa * 1.15) return 'P'
        if (spa >= atk * 1.15) return 'S'
        return 'M'
    }

    fun roleOf(p: Mon): Role {
        val b = p.stats
        val style = styleOf(p)
        val bulk = b[0] + b[2] + b[4]
        val offense = max(b[1], b[3])
        val spe = b[5]
        val kind = if (style == 'P') "físico" else if (style == 'S') "especial" else "misto"
        if (spe >= 95 && offense >= 95) return Role("Atacante $kind rápido", "Ataque primeiro e derrube antes de levar dano. Golpes de setup aumentam muito o estrago.")
        if (bulk >= 270 && offense < 95) return Role("Tanque / suporte", "Aguenta golpes. Use status (queimar, envenenar, paralisar) e recuperação.")
        if (bulk >= 260) return Role("Atacante $kind resistente", "Pode entrar em campo levando um golpe e revidar com força.")
        if (spe < 60 && offense >= 95) return Role("Atacante $kind lento", "Bate forte mas age por último. Entre depois que um aliado cair ou contra alvos lentos.")
        if (offense >= 85) return Role("Atacante $kind", "Foque em golpes STAB fortes e um golpe de cobertura.")
        return Role("Em desenvolvimento", "Atributos baixos ainda. Vale evoluir antes de confiar nele em lutas difíceis.")
    }

    // ---------- EVs and nature ----------
    val statShort = listOf("HP", "ATQ", "DEF", "AT.E", "DF.E", "VEL")
    private val natureNames = listOf(
        // minus: atk, def, spa, spd, spe
        listOf("Hardy", "Lonely", "Adamant", "Naughty", "Brave"), // plus atk
        listOf("Bold", "Docile", "Impish", "Lax", "Relaxed"), // plus def
        listOf("Modest", "Mild", "Bashful", "Rash", "Quiet"), // plus spa
        listOf("Calm", "Gentle", "Careful", "Quirky", "Sassy"), // plus spd
        listOf("Timid", "Hasty", "Jolly", "Naive", "Serious"), // plus spe
    )

    // Same split as roleOf. EVs stay after evolving, so a Pokémon that still evolves is planned as its strongest final form
    // (except in Little Cup, where it stays as it is).
    fun buildOf(p: Mon): EvBuild {
        val t = if (isFinal(p) || s.mode.level == "lc") p else finalForms(p).maxByOrNull { it.bst } ?: p
        val b = t.stats
        // Huge Power and Pure Power double Attack, so those Pokémon are built physical whatever their base stats say
        val power = t.abilities.any { it.trimStart('*') == "Huge Power" || it.trimStart('*') == "Pure Power" }
        val style = if (power) 'P' else styleOf(t)
        val bulk = b[0] + b[2] + b[4]
        val offense = max(if (power) b[1] * 2 else b[1], b[3])
        val spe = b[5]
        val off = if (style == 'P') 1 else if (style == 'S') 3 else if (b[1] >= b[3]) 1 else 3
        val unused = if (off == 1) 3 else 1
        val weak = if (b[2] <= b[4]) 2 else 4
        val strong = if (weak == 2) 4 else 2
        val drop = if (style == 'M') strong else unused // mixed attackers keep both attack stats
        val evs = IntArray(6)
        fun put(a: Int, c: Int, d: Int) { evs[a] = 252; evs[c] = 252; evs[d] = 4 }
        val (plus, minus) = when {
            spe >= 95 && offense >= 95 -> { put(off, 5, 0); 5 to drop }
            bulk >= 270 && offense < 95 -> { put(0, weak, strong); weak to if (style == 'P') 3 else 1 }
            bulk >= 260 -> { put(0, off, weak); off to drop }
            spe < 60 && offense >= 95 -> { put(0, off, weak); off to 5 }
            offense >= 85 -> { put(off, 5, 0); off to drop }
            else -> { put(0, 5, weak); 5 to unused }
        }
        return EvBuild(natureNames[plus - 1][minus - 1], plus, minus, evs, t)
    }

    fun evText(b: EvBuild) = (0 until 6).filter { b.evs[it] > 0 }
        .sortedWith(compareByDescending<Int> { b.evs[it] }.thenBy { it })
        .joinToString(" · ") { "${b.evs[it]} ${statShort[it]}" }

    fun natureText(b: EvBuild) = "${b.nature} (+${statShort[b.plus]}, −${statShort[b.minus]})"

    // ---------- battle mode ----------
    val isMulti get() = s.mode.format != "singles"
    val activeCount get() = when (s.mode.format) { "doubles" -> 2; "triples" -> 3; else -> 1 }
    val maxCore get() = max(1, min(5, s.mode.size - 1))

    fun battleLv(lv: Int) = when (s.mode.level) { "50" -> 50; "100" -> 100; "lc" -> 5; else -> lv }

    fun modeSummary(): String {
        val m = s.mode
        val size = if (m.pick < m.size) "${m.pick} de ${m.size}" else "${m.size}x${m.size}"
        return "${formatLabel[m.format]} $size · ${levelLabel[m.level]}" + (if (m.banLegends) " · sem lendários" else "") + (if (m.sleepClause) " · sem sono" else "")
    }

    fun modeShort(): String {
        val m = s.mode
        return "${formatLabel[m.format]} " + if (m.pick < m.size) "${m.pick} de ${m.size}" else "${m.size}x${m.size}"
    }

    fun presetMode(key: String): BattleMode {
        val p = presets.getValue(key)
        return BattleMode(key, p.format, p.size, p.pick, p.level, p.banLegends, p.sleepClause)
    }

    private val namesCache = ConcurrentHashMap<Int, Set<String>>()

    fun moveNames(p: Mon): Set<String> = namesCache.getOrPut(p.id) {
        val out = HashSet<String>()
        for (id in p.otherMoves) Dex.moves[id]?.let { out.add(it.name) }
        var i = 0
        while (i < p.levelMoves.size) { Dex.moves[p.levelMoves[i]]?.let { out.add(it.name) }; i += 2 }
        out
    }

    private val featCache = ConcurrentHashMap<Int, Feat>()

    fun featOf(p: Mon): Feat = featCache.getOrPut(p.id) {
        val n = moveNames(p)
        Feat(n.count { it in spread }, "Fake Out" in n, redirect.any { it in n }, speedCtrl.any { it in n }, "Earthquake" in n)
    }

    // ---------- moves ----------
    fun learnable(p: Mon, lv: Int?, tms: Boolean): List<PMove> {
        val res = ArrayList<PMove>()
        fun add(id: Int, src: String) {
            val info = Dex.moves[id] ?: return
            if (info.name == "Tera Blast") return
            if (s.mode.sleepClause && info.name in sleepMoves) return
            if (info.name == "Fake Out" && !isMulti) return
            res.add(PMove(info, src))
        }
        var i = 0
        while (i + 1 < p.levelMoves.size) {
            val id = p.levelMoves[i]
            val at = p.levelMoves[i + 1]
            if (lv == null || at <= lv) add(id, if (at <= 1) "Nv 1" else "Nv $at")
            i += 2
        }
        if (tms) for (id in p.otherMoves) add(id, "TM/tutor/ovo")
        return res
    }

    fun moveValue(p: Mon, mv: PMove): Double {
        val i = mv.info
        if (i.cls == 1) return 0.0
        val pw = if (i.power == -1) 65.0 else i.power.toDouble()
        val acc = if (i.accuracy > 100) 1.0 else i.accuracy / 100.0
        val stab = if (i.type in p.types) 1.5 else 1.0
        val stat = (if (i.cls == 2) p.stats[1] else p.stats[3]).toDouble()
        val best = max(p.stats[1], p.stats[3]).toDouble()
        val drawback = if (i.name in statDrop) 0.75 else 1.0
        val spreadBonus = if (isMulti && i.name in spread) 1.3 else 1.0
        return pw * acc * stab * (stat / best) * (if (i.priority > 0) 0.9 else 1.0) * drawback * spreadBonus
    }

    fun pickMoves(p: Mon, lv: Int?, tms: Boolean): List<PMove> {
        val pool = learnable(p, lv, tms)
        val atks = pool.filter { it.info.cls != 1 && (it.info.power == -1 || it.info.power >= 40) }
        atks.forEach { it.value = moveValue(p, it) }
        val chosen = ArrayList<PMove>()
        fun take(m: PMove?) { if (m != null && chosen.none { it.info.id == m.info.id }) chosen.add(m) }

        for (t in p.types) take(atks.filter { it.type == t }.maxByOrNull { it.value })
        val style = styleOf(p)
        val setupList = if (style == 'P') setupP else if (style == 'S') setupS else setupP + setupS
        val setup = setupList.firstNotNullOfOrNull { n -> pool.firstOrNull { it.name == n } }
        fun coverSlots() = 4 - chosen.size - (if (setup != null && setup !in chosen) 1 else 0)

        val hits = HashSet<String>()
        fun markHits(m: PMove) { for (d in all) if (eff(m.type, d) >= 2) hits.add(d) }
        chosen.forEach { markHits(it) }
        while (coverSlots() > 0) {
            var best: PMove? = null
            var bestScore = 0.0
            for (m in atks) {
                if (chosen.any { it.info.id == m.info.id || it.type == m.type }) continue
                val fresh = all.count { eff(m.type, it) >= 2 && it !in hits }
                val score = fresh * 30 + m.value
                if (fresh > 0 && score > bestScore) { best = m; bestScore = score }
            }
            if (best == null) break
            take(best); markHits(best)
        }
        if (setup != null) take(setup)
        val rest = atks.sortedByDescending { it.value }
        for (m in rest) { if (chosen.size >= 4) break; if (chosen.none { it.type == m.type }) take(m) }
        for (m in rest) { if (chosen.size >= 4) break; if (chosen.count { it.type == m.type } < 2) take(m) }
        if (!isMulti) return chosen.take(4)

        // doubles/triples: two attacks, one support move, Protect
        fun has(n: String) = pool.firstOrNull { it.name == n }
        val utilOrder = if (p.stats[5] <= 60)
            listOf("Trick Room", "Follow Me", "Rage Powder", "Fake Out", "Helping Hand", "Icy Wind", "Electroweb", "Wide Guard", "Tailwind")
        else
            listOf("Fake Out", "Tailwind", "Follow Me", "Rage Powder", "Icy Wind", "Electroweb", "Snarl", "Helping Hand", "Wide Guard")
        val out = chosen.filter { it.info.cls != 1 && it.type in p.types }.take(2).toMutableList()
        val util = utilOrder.map { has(it) }.firstOrNull { it != null && it !in out }
        if (util != null) out.add(util)
        has("Protect")?.let { out.add(it) }
        for (m in chosen + rest) {
            if (out.size >= 4) break
            if (m.info.cls != 1 && m !in out && out.none { it.type == m.type }) out.add(m)
        }
        return out.take(4)
    }

    // ---------- team analysis ----------
    fun defenseTable(members: List<Member>): List<DefRow> = all.mapIndexed { ai, atk ->
        val mults = DoubleArray(members.size) { members[it].dv[ai] }
        DefRow(atk, mults, mults.count { it > 1 }, mults.count { it < 1 })
    }

    fun scoreTeam(members: List<Member>): TeamScore {
        val table = defenseTable(members)
        var defPen = 0.0
        val holes = ArrayList<String>()
        for (r in table) {
            val gap = r.weak - r.res
            if (gap > 0) defPen += gap * gap * 3
            if (r.weak >= 3) defPen += 6
            if (r.weak >= 2 && r.res == 0) holes.add(r.atk)
        }
        val stab = members.flatMap { it.p.types }.distinct()
        val covered = all.filter { d -> stab.any { t -> eff(t, d) >= 2 } }
        val typeCount = LinkedHashMap<String, Int>()
        for (x in members) for (t in x.p.types) typeCount[t] = (typeCount[t] ?: 0) + 1
        val dupPen = typeCount.values.sumOf { n -> if (n > 1) ((n - 1) * (n - 1) * 5).toDouble() else 0.0 }
        val avgBst = if (members.isEmpty()) 0.0 else members.map { it.p.bst }.average()
        val styles = members.map { styleOf(it.p) }
        val balance = if (('P' in styles || 'M' in styles) && ('S' in styles || 'M' in styles)) 6.0 else 0.0
        val fast = members.count { it.p.stats[5] >= 90 }
        val score = covered.size * 4 - defPen - dupPen + avgBst / 10 + balance + min(fast, 2) * 3
        return TeamScore(score, defPen, dupPen, avgBst, covered, holes, table, typeCount)
    }

    private fun <T> combos(items: List<T>, k: Int, start: Int = 0, acc: MutableList<T> = ArrayList(), out: MutableList<List<T>> = ArrayList()): List<List<T>> {
        if (acc.size == k) { out.add(acc.toList()); return out }
        for (i in start..items.size - (k - acc.size)) {
            acc.add(items[i])
            combos(items, k, i + 1, acc, out)
            acc.removeAt(acc.size - 1)
        }
        return out
    }

    fun bestSubset(members: List<Member>, k: Int): List<Member> {
        if (members.size <= k) return members
        var best = members
        var bestS = -Double.MAX_VALUE
        for (c in combos(members, k)) {
            val sc = scoreTeam(c)
            val v = sc.score + sc.avgBst / 5
            if (v > bestS) { bestS = v; best = c }
        }
        return best
    }

    fun names(xs: Iterable<Member>) = xs.joinToString(", ") { "**${it.p.name}**" }
    private fun ptList(types: Iterable<String>) = types.joinToString(", ") { pt.getValue(it) }

    fun teamAlerts(members: List<Member>, sc: TeamScore): List<Alert> {
        val alerts = ArrayList<Alert>()
        for (x in members) {
            val ready = nextEvos(x.p).filter { it.evo != null && it.evo.level > 0 && x.lv >= it.evo.level }
            if (ready.isNotEmpty()) alerts.add(Alert("good", "**${x.p.name}** já pode evoluir para **${ready.joinToString(" / ") { it.name }}** (nível ${ready[0].evo!!.level}). Abra o menu do time no jogo para confirmar a evolução."))
            val items = nextEvos(x.p).filter { it.evo?.item != null }
            if (items.isNotEmpty()) alerts.add(Alert("neutral", "**${x.p.name}** evolui com item: ${items.joinToString(" ou ") { "${it.evo!!.item} → **${it.name}**" }}."))
        }
        for ((t, n) in sc.typeCount) if (n >= 3) alerts.add(Alert("bad", "$n Pokémon do tipo ${pt[t]} no time. Isso concentra as mesmas fraquezas: ${ptList(all.filter { eff(it, t) >= 2 })}."))
        for (t in sc.holes) {
            val r = sc.table.first { it.atk == t }
            alerts.add(Alert(if (r.weak >= 3) "bad" else "warn", "Golpes ${pt[t]} acertam ${r.weak} do time em cheio e ninguém resiste."))
        }
        if (members.isEmpty()) alerts.add(Alert("warn", "Seu time está vazio. Adicione Pokémon abaixo."))

        val md = s.mode
        if (members.size > md.size) alerts.add(Alert("bad", "O modo ${modeSummary()} usa ${md.size} Pokémon e seu time tem ${members.size}. Remova ${members.size - md.size}."))
        else if (members.isNotEmpty() && members.size < md.size) alerts.add(Alert("warn", "O modo pede ${md.size} Pokémon e seu time tem ${members.size}."))
        val banned = members.filter { md.banLegends && (it.p.legendary || it.p.mythical) }
        if (banned.isNotEmpty()) alerts.add(Alert("bad", "Lendários e míticos estão proibidos neste modo: ${names(banned)}."))
        if (md.level == "lc") {
            val notBasic = members.filter { !isBasic(it.p) }
            if (notBasic.isNotEmpty()) alerts.add(Alert("bad", "Little Cup só aceita Pokémon no 1º estágio que ainda evoluem: ${names(notBasic)} não vale."))
        }
        if (md.level != "free") alerts.add(Alert("neutral", "${levelLabel[md.level]}: os golpes sugeridos consideram o nível ${battleLv(1)}."))
        if (md.pick < md.size && members.size > md.pick) alerts.add(Alert("neutral", "Você leva ${md.pick} de ${md.size}. Melhor grupo geral: ${names(bestSubset(members, md.pick))}. Na aba Batalha dá para escolher contra um adversário específico."))
        if (isMulti && members.isNotEmpty()) {
            val lacks = ArrayList<String>()
            if (members.none { featOf(it.p).fake }) lacks.add("Fake Out")
            if (members.none { featOf(it.p).speed }) lacks.add("controle de velocidade (Tailwind, Trick Room, Icy Wind)")
            if (members.none { featOf(it.p).redirect }) lacks.add("alguém com Follow Me ou Rage Powder")
            if (members.none { it.ab == "Intimidate" }) lacks.add("Intimidate")
            val f = formatLabel.getValue(md.format).lowercase()
            alerts.add(
                if (lacks.isNotEmpty()) Alert("warn", "Para $f, falta no time: ${lacks.joinToString(", ")}.")
                else Alert("good", "Time pronto para $f: tem Fake Out, controle de velocidade, redirecionamento e Intimidate."),
            )
        }
        return alerts
    }

    // best suggested move per member vs each defending type
    fun offense(members: List<Member>): List<Coverage> = all.map { d ->
        var best = 0.0
        var who: MutableList<String> = ArrayList()
        for (x in members) {
            val m = x.moves.filter { it.info.cls != 1 }.maxOfOrNull { eff(it.type, d) } ?: 0.0
            if (m > best) { best = m; who = mutableListOf(x.p.name) } else if (m == best && m >= 2) who.add(x.p.name)
        }
        Coverage(d, best, who)
    }

    fun suggestions(members: List<Member>, n: Int): List<Suggestion> {
        val baseScore = scoreTeam(members).score
        val have = members.map { it.p.species }.toSet()
        class Option(val p: Mon, val gain: Double, val replace: String?)
        val options = ArrayList<Option>()
        for (p in Dex.all) {
            if (!isFinal(p) || p.species in have || p.bst < 420) continue
            if ((!s.legends || s.mode.banLegends) && (p.legendary || p.mythical)) continue
            val cand = Member(p, defaultAbility(p))
            var gain: Double
            var replace: String? = null
            if (members.size < 6) gain = scoreTeam(members + cand).score - baseScore
            else {
                gain = -Double.MAX_VALUE
                for (i in members.indices) {
                    val g = scoreTeam(members.mapIndexed { j, y -> if (j == i) cand else y }).score - baseScore
                    if (g > gain) { gain = g; replace = members[i].p.name }
                }
            }
            options.add(Option(p, gain, replace))
        }
        val holes = scoreTeam(members).holes
        return options.sortedByDescending { it.gain }.take(n).map { o ->
            val parts = ArrayList<String>()
            val resists = holes.filter { defMult(o.p, defaultAbility(o.p), it) < 1 }
            if (resists.isNotEmpty()) parts.add("resiste a " + ptList(resists))
            val newHits = all.filter { d -> o.p.types.any { eff(it, d) >= 2 } && members.none { x -> x.p.types.any { eff(it, d) >= 2 } } }
            if (newHits.isNotEmpty()) parts.add("STAB novo contra " + ptList(newHits))
            parts.add("BST ${o.p.bst}")
            if (o.replace != null) parts.add("no lugar de " + o.replace)
            val pre = Dex.all.firstOrNull { it.chain == o.p.chain && it.from == 0 && !it.regional }
            if (pre != null && pre.species != o.p.species) parts.add("começa como " + pre.name)
            Suggestion(o.p, parts.joinToString(" · "))
        }
    }

    // ---------- box generator ----------
    fun genPool(cfg: BoxConfig): List<Mon> {
        val lc = s.mode.level == "lc"
        val pool = if (cfg["wishOnly"])
            s.wish.mapNotNull { Dex.byId[it] }.flatMap { if (lc) listOf(it) else finalForms(it) }.distinctBy { it.id }
        else
            Dex.all.filter { (if (lc) isBasic(it) else isFinal(it)) && it.levelMoves.size + it.otherMoves.size > 0 }
        return pool.filter { p ->
            when {
                (!cfg["legends"] || s.mode.banLegends) && (p.legendary || p.mythical) -> false
                lc && !isBasic(p) -> false
                cfg["mono"] && cfg.monoType !in p.types -> false
                cfg["gen"] && p.gen != cfg.genNum -> false
                cfg["competitive"] && !cfg["wishOnly"] && !lc && p.bst < 440 -> false
                else -> true
            }
        }
    }

    fun bestAbility(p: Mon, cfg: BoxConfig): String {
        val names = abilityNames(p)
        if (cfg["weather"]) {
            val w = weathers.getValue(cfg.weatherKind)
            val hit = names.firstOrNull { it in w.setters } ?: names.firstOrNull { it in w.abusers }
            if (hit != null) return hit
        }
        if (isMulti && "Intimidate" in names) return "Intimidate"
        return defaultAbility(p)
    }

    private fun scoringOn(cfg: BoxConfig) = listOf("synergy", "competitive", "balanced", "fast", "trickroom", "weather", "wishPrio", "noDupTypes").any { cfg[it] }
    private fun learnsMove(p: Mon, name: String) = name in moveNames(p)

    fun genScore(members: List<Member>, cfg: BoxConfig): Double {
        val sc = scoreTeam(members)
        var v = 0.0
        val dup = sc.typeCount.values.sumOf { n -> if (n > 1) ((n - 1) * (n - 1)).toDouble() else 0.0 }
        if (!cfg["mono"]) v -= dup * (if (cfg["noDupTypes"]) 40 else 4)
        if (isMulti) {
            var fake = false
            var redirectOn = false
            var speed = false
            val gi = TypeChart.index("ground")
            val eqUser = members.any { featOf(it.p).eq && "ground" in it.p.types }
            for (x in members) {
                val f = featOf(x.p)
                v += min(f.spread, 2) * 3
                if (x.ab == "Intimidate") v += 6
                if (x.ab == "Prankster") v += 3
                if (x.ab == "Friend Guard" || x.ab == "Telepathy") v += 2
                if (eqUser && x.dv[gi] == 0.0) v += 3
                fake = fake || f.fake; redirectOn = redirectOn || f.redirect; speed = speed || f.speed
            }
            v += (if (fake) 10 else 0) + (if (redirectOn) 8 else 0) + (if (speed) 8 else 0)
        }
        // when you only bring part of the team, each member has to hold up alone
        if (s.mode.pick < s.mode.size) v += sc.avgBst / 10
        if (cfg["synergy"]) {
            v += sc.covered.size * 5 - sc.defPen * 1.2 - sc.holes.size * 6
            for (a in members) for (b in members) {
                if (a === b) continue
                val da = a.dv
                val db = b.dv
                for (i in 0 until 18) if (da[i] > 1 && db[i] < 1) v += 0.6
            }
        } else v += sc.covered.size * 1.5 - sc.defPen * 0.3
        if (cfg["competitive"]) {
            for (x in members) v += (x.p.bst - 480) / 8.0 + (if (x.p.name in meta) 14 else 0) + (if (x.p.stats.drop(1).max() >= 110) 3 else 0)
        } else v += sc.avgBst / 8
        if (cfg["balanced"]) {
            val st = members.map { styleOf(it.p) }
            if (st.any { it != 'S' }) v += 6
            if (st.any { it != 'P' }) v += 6
            if (members.any { it.p.stats[0] + it.p.stats[2] + it.p.stats[4] >= 270 }) v += 6
        }
        if (cfg["fast"]) for (x in members) v += if (x.p.stats[5] >= 100) 8 else if (x.p.stats[5] >= 85) 3 else -3
        if (cfg["trickroom"]) {
            for (x in members) v += if (x.p.stats[5] <= 55 && max(x.p.stats[1], x.p.stats[3]) >= 100) 12 else if (x.p.stats[5] >= 85) -16 else 0
            if (members.any { learnsMove(it.p, "Trick Room") }) v += 30
        }
        if (cfg["weather"]) {
            val w = weathers.getValue(cfg.weatherKind)
            if (members.any { it.ab in w.setters }) v += 45
            for (x in members) {
                if (x.ab in w.abusers) v += 14
                if (x.p.types.any { it in w.good }) v += 6
                if (x.p.types.any { it in w.bad }) v -= 6
            }
        }
        if (cfg["wishPrio"]) for (x in members) if (s.wish.any { Dex.byId[it]?.chain == x.p.chain }) v += 15
        return v
    }

    private fun keyOf(team: List<Member>) = team.map { it.p.id }.sorted().joinToString(",")

    fun generateBoxes(cfg: BoxConfig, shown: MutableSet<String>, fixedOverride: List<Member>? = null): GenResult? {
        val pool = genPool(cfg).map { Member(it, bestAbility(it, cfg)) }
        val fixed = fixedOverride ?: if (cfg["keepTeam"]) resolveTeam().map { Member(it.p, it.ab, it.lv, it.index, kept = true) } else emptyList()
        val target = s.mode.size
        val need = target - fixed.size
        if (need <= 0) return GenResult(fixed, genScore(fixed, cfg), pool.size, keyOf(fixed), scoreTeam(fixed))
        val fixedChains = fixed.map { it.p.chain }.toSet()
        val free = pool.filter { it.p.chain !in fixedChains }
        if (free.isEmpty()) return null

        if (cfg["random"] && !scoringOn(cfg)) {
            val pick = ArrayList<Member>()
            val used = HashSet<Int>()
            for (c in free.shuffled(rng)) {
                if (pick.size >= need) break
                if (used.add(c.p.chain)) pick.add(c)
            }
            val team = fixed + pick
            return GenResult(team, 0.0, pool.size, keyOf(team), scoreTeam(team))
        }
        val restarts = if (cfg["random"]) 8 else 14
        val topK = if (cfg["random"]) 25 else 3
        var best: GenResult? = null
        for (r in 0 until restarts) {
            val team = fixed.toMutableList()
            val used = HashSet(fixedChains)
            var first = true
            while (team.size < target) {
                val scored = ArrayList<Pair<Member, Double>>()
                for (c in free) {
                    if (c.p.chain in used) continue
                    team.add(c)
                    scored.add(c to genScore(team, cfg) + (if (cfg["random"]) rng.nextDouble() * 25 else rng.nextDouble() * 2))
                    team.removeAt(team.size - 1)
                }
                if (scored.isEmpty()) break
                scored.sortByDescending { it.second }
                val k = if (first) min(scored.size, if (cfg["random"]) 60 else 20) else min(scored.size, topK)
                val choice = scored[rng.nextInt(k)].first
                first = false
                team.add(choice); used.add(choice.p.chain)
            }
            val key = keyOf(team)
            if (key in shown && r < restarts - 1) continue
            val v = genScore(team, cfg)
            if (best == null || v > best.score) best = GenResult(team.toList(), v, pool.size, key, scoreTeam(team))
        }
        if (best != null) shown.add(best.key)
        return best
    }

    fun explainTeam(team: List<Member>, cfg: BoxConfig): List<String> {
        val out = ArrayList<String>()
        val sc = scoreTeam(team)
        val md = s.mode
        if (team.size < md.size) out.add("Só deu para montar ${team.size} Pokémon com essas caixas" + (if (cfg["wishOnly"]) " (sua lista não tem ${md.size} linhas evolutivas diferentes; adicione mais na aba Montar)" else "") + ".")
        if (md.pick < md.size && team.size >= md.pick) out.add("Neste modo você leva ${md.pick} de ${md.size}. Melhor grupo geral: ${names(bestSubset(team, md.pick))}. Ajuste conforme o adversário na aba Batalha.")
        if (isMulti) {
            val parts = ArrayList<String>()
            fun who(take: Int = 6, f: (Feat) -> Boolean) = team.filter { f(featOf(it.p)) }.take(take).joinToString(", ") { it.p.name }
            val fake = who { it.fake }
            val speed = who(3) { it.speed }
            val redir = who { it.redirect }
            if (fake != "") parts.add("Fake Out: $fake")
            if (speed != "") parts.add("controle de velocidade: $speed")
            if (redir != "") parts.add("puxa golpes (Follow Me/Rage Powder): $redir")
            val intim = team.filter { it.ab == "Intimidate" }.joinToString(", ") { it.p.name }
            if (intim != "") parts.add("Intimidate: $intim")
            if (parts.isNotEmpty()) out.add("Para ${formatLabel.getValue(md.format).lowercase()}: ${parts.joinToString(" · ")}.")
        }
        if (cfg["weather"]) {
            val w = weathers.getValue(cfg.weatherKind)
            val setter = team.firstOrNull { it.ab in w.setters }
            val abusers = team.filter { it.ab in w.abusers }
            out.add(if (setter != null) "**${setter.p.name}** liga ${w.label.lowercase()} com ${setter.ab} ao entrar." else "Nenhum Pokémon da seleção liga ${w.label.lowercase()} sozinho. Use o golpe de clima ou tire filtros.")
            if (abusers.isNotEmpty()) out.add("Aproveitam o clima: " + abusers.joinToString(", ") { "**${it.p.name}** (${it.ab})" } + ".")
        }
        if (cfg["trickroom"]) {
            val setter = team.filter { learnsMove(it.p, "Trick Room") }
            out.add(if (setter.isNotEmpty()) "Trick Room com ${names(setter)}. Depois dele, os mais lentos atacam primeiro por 5 turnos." else "Ninguém aqui aprende Trick Room. Tire alguns filtros para achar um.")
        }
        if (cfg["synergy"] || cfg["competitive"]) {
            out.add(if (sc.holes.isNotEmpty()) "Brechas restantes: " + ptList(sc.holes) + "." else "Nenhum tipo de golpe acerta 2 ou mais do time sem alguém resistir.")
            out.add("Os STABs do time acertam ${sc.covered.size} de 18 tipos super efetivo.")
        }
        val fast = team.filter { it.p.stats[5] >= 100 }.map { it.p.name }
        if (fast.isNotEmpty()) out.add("Mais rápidos: " + fast.joinToString(", ") + ".")
        val tanks = team.filter { it.p.stats[0] + it.p.stats[2] + it.p.stats[4] >= 270 }.map { it.p.name }
        if (tanks.isNotEmpty()) out.add("Seguram dano: " + tanks.joinToString(", ") + ".")
        return out
    }

    // ---------- build around chosen ----------
    fun supportReason(f: Member, cores: List<Member>): String {
        val parts = ArrayList<String>()
        val guards = all.filterIndexed { i, _ -> f.dv[i] < 1 && cores.any { it.dv[i] > 1 } }
        if (guards.isNotEmpty()) parts.add("aguenta golpes ${ptList(guards)}, que ameaçam ${if (cores.size > 1) "seus escolhidos" else cores[0].p.name}")
        val coreStab = cores.flatMap { it.p.types }.distinct()
        val walls = all.filter { d -> coreStab.all { eff(it, d) <= 1 } }
        val breaks = walls.filter { d -> f.p.types.any { eff(it, d) >= 2 } }
        if (breaks.isNotEmpty()) parts.add("acerta em cheio ${ptList(breaks)}, que seus escolhidos não acertam")
        return if (parts.isNotEmpty()) parts.joinToString("; ") else "reforça os atributos e o papel que faltava no time"
    }

    fun buildAround(shown: MutableSet<String>): List<GenResult> {
        val cfg = s.build
        val chosen = s.wish.take(maxCore).mapNotNull { Dex.byId[it] }
        val options = chosen.map { if (s.finals && s.mode.level != "lc") finalForms(it) else listOf(it) }
        var combos: List<List<Mon>> = listOf(emptyList())
        for (o in options) combos = combos.flatMap { a -> o.map { a + it } }
        var core: List<Member>? = null
        var coreScore = -Double.MAX_VALUE
        for (combo in combos.take(32)) {
            val fixed = combo.map { Member(it, bestAbility(it, cfg), core = true) }
            val v = genScore(fixed, cfg)
            if (v > coreScore) { coreScore = v; core = fixed }
        }
        val results = ArrayList<GenResult>()
        for (n in 0 until 3) {
            val r = generateBoxes(cfg, shown, core) ?: break
            if (results.any { it.key == r.key }) break
            results.add(r)
        }
        return results
    }

    // ---------- battle ----------
    fun rankVs(members: List<Member>, oppTypes: List<String>, oppMon: Mon?): List<Ranked> = members.map { x ->
        val mv = pickMoves(x.p, battleLv(x.lv), s.tms).filter { it.info.cls != 1 }
        var best: PMove? = null
        var bestDmg = 0.0
        var bestMult = 0.0
        for (m in mv) {
            val mult = oppTypes.fold(1.0) { a, t -> a * eff(m.type, t) }
            val dmg = moveValue(x.p, m) * mult
            if (dmg > bestDmg) { bestDmg = dmg; best = m; bestMult = mult }
        }
        val incoming = oppTypes.maxOf { defMult(x.p, x.ab, it) }
        val speed = when {
            oppMon == null -> null
            x.p.stats[5] > oppMon.stats[5] -> "mais rápido"
            x.p.stats[5] < oppMon.stats[5] -> "mais lento"
            else -> "empata em velocidade"
        }
        val score = (bestDmg * max(x.p.stats[1], x.p.stats[3]) / 100) / max(0.25, incoming)
        Ranked(x, best, bestMult, incoming, speed, score)
    }.sortedByDescending { it.score }
}
