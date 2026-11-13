package io.github.carlosgguilherme.paletmon

import io.github.carlosgguilherme.paletmon.data.Dex
import io.github.carlosgguilherme.paletmon.data.Mon
import io.github.carlosgguilherme.paletmon.logic.AppState
import io.github.carlosgguilherme.paletmon.logic.BoxConfig
import io.github.carlosgguilherme.paletmon.logic.Engine
import io.github.carlosgguilherme.paletmon.logic.TeamSlot
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.BeforeClass
import org.junit.Test
import java.io.File
import kotlin.math.max
import kotlin.math.min

// Builds the same lines as PaletMon.Snap/Golden.cs and compares them with the C# engine's output.
// CI generates that output and passes its path in PALETMON_GOLDEN:
//   dotnet run --project PaletMon.Snap -- --golden golden.txt
class ParityTest {
    private val lines = ArrayList<String>()

    private fun n(v: Double) = Math.rint(v * 10000).toLong().toString()
    private fun b(v: Boolean) = if (v) "1" else "0"
    private fun j(xs: Iterable<String>) = xs.joinToString(",")
    private fun ids(xs: Iterable<Mon>) = j(xs.map { it.id.toString() })
    private fun add(vararg parts: Any?) { lines.add(parts.joinToString("|")) }

    private fun applyPreset(preset: String, wish: List<Int> = emptyList()) {
        Engine.s = AppState(wish = wish, mode = Engine.presetMode(preset))
    }

    private fun mon(p: Mon) {
        val role = Engine.roleOf(p)
        val f = Engine.featOf(p)
        add(
            "mon", p.id, p.name, p.bst, role.name, role.tip, Engine.styleOf(p), Engine.defaultAbility(p), j(Engine.abilityNames(p)),
            ids(Engine.nextEvos(p)), ids(Engine.finalForms(p)), Engine.evoText(p.evo), b(Engine.isFinal(p)), b(Engine.isBasic(p)),
            f.spread, b(f.fake), b(f.redirect), b(f.speed), b(f.eq),
        )
        ev("ev", p)
    }

    private fun ev(tag: String, p: Mon) {
        val b = Engine.buildOf(p)
        add("ev", tag, p.id, b.target.id, Engine.evText(b), Engine.natureText(b))
    }

    private fun moves(preset: String, p: Mon) {
        for (lv in listOf(null, 5, 30, 60, 100)) {
            for (tms in listOf(true, false)) {
                add("moves", preset, p.id, lv?.toString() ?: "-", b(tms), j(Engine.pickMoves(p, lv, tms).map { "${it.name}:${it.src}:${n(Engine.moveValue(p, it))}" }))
            }
        }
    }

    private fun pool(preset: String, name: String, cfg: BoxConfig) {
        val pool = Engine.genPool(cfg)
        var sum = 0L
        pool.forEachIndexed { i, p -> sum = (sum + p.id.toLong() * (i + 1)) % 1000000007 }
        add("pool", preset, name, pool.size, sum, j(pool.filterIndexed { i, _ -> i % 7 == 0 }.map { Engine.bestAbility(it, cfg) }))
    }

    private fun teamLines(tag: String, team: List<TeamSlot>) {
        Engine.s = Engine.s.copy(team = team)
        val s = Engine.s
        val members = Engine.resolveTeam()
        for (x in members) x.moves = Engine.pickMoves(x.p, Engine.battleLv(x.lv), s.tms)
        val sc = Engine.scoreTeam(members)

        for (x in members) ev(tag, x.p)
        add("score", tag, n(sc.score), n(sc.defPen), n(sc.dupPen), n(sc.avgBst), j(sc.covered), j(sc.holes), j(sc.typeCount.map { "${it.key}=${it.value}" }))
        for (r in sc.table) add("def", tag, r.atk, j(r.mults.map(::n)), r.weak, r.res)
        for (a in Engine.teamAlerts(members, sc)) add("alert", tag, a.kind, a.text)
        for (o in Engine.offense(members)) add("off", tag, o.type, n(o.best), j(o.who))
        for (x in Engine.suggestions(members, 6)) add("sug", tag, x.p.id, x.why)
        if (members.isEmpty()) return

        add("subset", tag, Engine.names(Engine.bestSubset(members, min(s.mode.pick, members.size))))
        for ((name, types, opp) in opponents()) {
            for (r in Engine.rankVs(members, types, opp)) {
                add("rank", tag, name, r.x.p.id, r.best?.name ?: "-", n(r.mult), n(r.incoming), r.speed ?: "-", n(r.score))
            }
        }
        for ((name, cfg) in configs()) {
            add("gs", tag, name, n(Engine.genScore(members, cfg)))
            for (w in Engine.explainTeam(members, cfg)) add("why", tag, name, w)
        }
        val cores = members.take(max(1, members.size / 2))
        for (x in members.drop(cores.size)) add("sup", tag, x.p.id, Engine.supportReason(x, cores))
    }

    private fun opponents(): List<Triple<String, List<String>, Mon?>> {
        val list = mutableListOf<Triple<String, List<String>, Mon?>>(
            Triple("water", listOf("water"), null),
            Triple("fire-flying", listOf("fire", "flying"), null),
            Triple("ghost-dark", listOf("ghost", "dark"), null),
        )
        for (name in listOf("Gyarados", "Garchomp", "Shuckle")) {
            val m = Dex.find(name)!!
            list.add(Triple(name, m.types, m))
        }
        return list
    }

    private fun configs(): List<Pair<String, BoxConfig>> {
        val list = Engine.boxes.map { it.key to BoxConfig.of(it.key) }.toMutableList()
        list.add("syn+bal" to BoxConfig.of("synergy", "balanced"))
        list.add("comp+fast" to BoxConfig.of("competitive", "fast"))
        list.add("tr+legends" to BoxConfig.of("trickroom", "legends"))
        list.add("wish+syn" to BoxConfig.of("wishOnly", "synergy"))
        for (w in listOf("rain", "sun", "sand", "snow")) list.add("weather-$w" to BoxConfig.of("weather").copy(weatherKind = w))
        for (t in listOf("water", "dragon", "steel")) list.add("mono-$t" to BoxConfig.of("mono", "synergy").copy(monoType = t))
        for (g in listOf(1, 4, 9)) list.add("gen-$g" to BoxConfig.of("gen").copy(genNum = g))
        return list
    }

    private fun teams(): List<List<TeamSlot>> {
        val teams = mutableListOf(AppState().team, emptyList())
        val n = Dex.all.size
        for (k in 0 until 12) {
            val size = 1 + k % 6
            teams.add(
                (0 until size).map { i ->
                    val p = Dex.all[(k * 37 + i * 131 + 11) % n]
                    val ab = if ((k + i) % 3 == 0) p.abilities.last().trimStart('*') else null
                    TeamSlot(p.id, 5 + (k * 13 + i * 7) % 95, ab)
                },
            )
        }
        return teams
    }

    private fun goldenFile(): File {
        val path = System.getenv("PALETMON_GOLDEN") ?: error("Defina PALETMON_GOLDEN com o arquivo gerado por PaletMon.Snap --golden")
        return File(path)
    }

    @Test
    fun sameResultsAsTheDesktopEngine() {
        Engine.s = AppState()
        for (p in Dex.all) mon(p)
        for (preset in listOf("livre", "vgc", "comp")) {
            applyPreset(preset)
            for (p in Dex.all) moves(preset, p)
        }
        val wish = listOf(445, 282, 6, 130, 25)
        for (preset in Engine.presets.keys) {
            applyPreset(preset, wish)
            add("mode", preset, Engine.modeSummary(), Engine.maxCore, Engine.activeCount, Engine.battleLv(37))
            for ((name, cfg) in configs()) pool(preset, name, cfg)
            teams().forEachIndexed { t, team -> teamLines("$preset/$t", team) }
        }

        // the phone's tab is called just "Montar"
        val expected = goldenFile().readLines().map { it.replace("aba Montar time", "aba Montar") }
        val different = expected.indices.filter { it >= lines.size || lines[it] != expected[it] }
        val report = different.take(15).joinToString("\n") { "C#:     ${expected[it]}\nKotlin: ${lines.getOrNull(it)}" }
        assertEquals("linhas: C# ${expected.size}, Kotlin ${lines.size}\n$report", 0, different.size)
        assertEquals(expected.size, lines.size)
        println("${lines.size} linhas iguais ao motor C#")
    }

    @Test
    fun readsTheTeamSavedByThePreviousApp() {
        val dir = File.createTempFile("paletmon", "").apply { delete(); mkdirs() }
        val file = AppState.file(dir)
        file.parentFile!!.mkdirs()
        File(goldenFile().path + ".state.json").copyTo(file)

        val s = AppState.load(dir)
        assertEquals(listOf(TeamSlot(445, 61, "Rough Skin"), TeamSlot(6, 36, null)), s.team)
        assertEquals(listOf(282), s.wish)
        assertEquals(false, s.tms)
        assertEquals(false, s.finals)
        assertEquals("batalha", s.tab)
        assertEquals("Gyarados", s.opp.mon)
        assertEquals("sun", s.gen.weatherKind)
        assertEquals(4, s.gen.genNum)
        assertTrue(s.gen["competitive"] && s.gen["weather"])
        assertEquals("doubles", s.mode.format)
        assertEquals(4, s.mode.pick)
        assertTrue(s.mode.banLegends)
    }

    @Test
    fun generatedTeamsRespectTheBoxes() {
        for (preset in Engine.presets.keys) {
            applyPreset(preset)
            val md = Engine.s.mode
            for (round in 0 until 12) {
                val cfgs = listOf(
                    BoxConfig.of("synergy", "balanced"),
                    BoxConfig.of("random"),
                    BoxConfig.of("competitive"),
                    BoxConfig.of("mono", "synergy").copy(monoType = "water"),
                    BoxConfig.of("gen", "balanced").copy(genNum = 3),
                    BoxConfig.of("noDupTypes"),
                )
                for (cfg in cfgs) {
                    val r = Engine.generateBoxes(cfg, HashSet()) ?: continue
                    assertTrue(r.team.size <= md.size)
                    assertEquals(r.team.size, r.team.map { it.p.chain }.distinct().size)
                    for (z in r.team) {
                        if (cfg["mono"]) assertTrue("water" in z.p.types)
                        if (cfg["gen"]) assertEquals(3, z.p.gen)
                        assertTrue(!z.p.legendary && !z.p.mythical)
                        if (md.level == "lc") assertTrue(Engine.isBasic(z.p))
                    }
                }
            }
        }
    }

    @Test
    fun buildAroundKeepsTheChosenPokemon() {
        applyPreset("livre", listOf(445, 282))
        val results = Engine.buildAround(HashSet())
        assertTrue(results.isNotEmpty())
        for (r in results) {
            assertEquals(2, r.team.count { it.core })
            assertEquals(6, r.team.size)
            assertTrue(r.team.any { it.p.name == "Garchomp" })
        }
    }

    companion object {
        @JvmStatic
        @BeforeClass
        fun loadDex() {
            val text = ParityTest::class.java.classLoader!!.getResource("dex.json")!!.readText()
            Dex.load(text)
        }
    }
}
