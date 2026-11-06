package io.github.carlosgguilherme.paletmon.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import io.github.carlosgguilherme.paletmon.AppModel
import io.github.carlosgguilherme.paletmon.data.Dex
import io.github.carlosgguilherme.paletmon.logic.Alert
import io.github.carlosgguilherme.paletmon.logic.Engine
import io.github.carlosgguilherme.paletmon.logic.Opponent
import io.github.carlosgguilherme.paletmon.logic.Ranked
import io.github.carlosgguilherme.paletmon.logic.TypeChart

@Composable
private fun TypeButton(type: String, on: Boolean, tap: () -> Unit) {
    val c = typeColor(type)
    val dark = type in TypeChart.darkText
    val shape = RoundedCornerShape(99.dp)
    Box(
        Modifier.clip(shape).background(if (on) c else c.copy(alpha = 0.14f)).border(1.5.dp, c.copy(alpha = if (on) 1f else 0.55f), shape)
            .clickable(onClick = tap).padding(horizontal = 12.dp, vertical = 7.dp),
    ) {
        Txt(TypeChart.pt.getValue(type), 13f, FontWeight.Bold, if (!on) pm.fg else if (dark) Color(0xFF1B1B1B) else Color.White)
    }
}

private fun tapType(o: Opponent, t: String) = when (t) {
    o.t1 -> if (o.t2.isNotEmpty()) o.copy(t1 = o.t2, t2 = "") else o
    o.t2 -> o.copy(t2 = "")
    else -> o.copy(t2 = t)
}

private fun leadText(ranked: List<Ranked>): String? {
    val md = Engine.s.mode
    val active = Engine.activeCount
    if (md.pick >= ranked.size && active <= 1) return null
    val bring = ranked.take(minOf(md.pick, ranked.size))
    val leads = bring.take(active)
    val why = if (active <= 1) emptyList() else leads.mapNotNull { r ->
        val f = Engine.featOf(r.x.p)
        if (f.fake) "Fake Out" else if (f.speed) "controle de velocidade" else if (r.x.ab == "Intimidate") "Intimidate" else null
    }.distinct()
    return (if (md.pick < ranked.size) "Leve **${md.pick}**: ${Engine.names(bring.map { it.x })}. " else "") +
        "Comece com ${leads.joinToString(" + ") { "**${it.x.p.name}**" }}" +
        (if (why.isNotEmpty()) " (${why.joinToString(", ")})" else "") + "."
}

@Composable
private fun RankCard(pos: Int, r: Ranked) {
    val (kind, verdict) = when {
        r.best != null && r.mult >= 2 && r.incoming <= 1 -> "good" to "Ótima escolha"
        r.incoming >= 2 && (r.best == null || r.mult < 2) -> "bad" to "Evite"
        r.incoming >= 2 -> "warn" to "Arriscado"
        else -> "neutral" to "Ok"
    }
    val detail = (if (r.best != null) "Use **${r.best.name}** (${TypeChart.pt[r.best.type]}, ×${TypeChart.mult(r.mult)})" else "Sem golpe de dano") +
        " · recebe até ×${TypeChart.mult(r.incoming)} de STAB" + (if (r.speed != null) " · ${r.speed}" else "")
    Card(pad = 12.dp) {
        Row {
            Portrait(r.x.p, 54.dp)
            Spacer(Modifier.width(12.dp))
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Rich("**$pos. ${r.x.p.name}**  Nv ${Engine.battleLv(r.x.lv)}", 15f, modifier = Modifier.weight(1f))
                    Pill(verdict, kind)
                }
                Rich(detail, 13f)
            }
        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun BattleScreen(model: AppModel, ui: UiState) {
    val st = model.state
    val o = st.opp
    val oppMon = if (o.mode == "mon" && o.mon.isNotBlank()) Dex.find(o.mon) else null
    val oppTypes = when {
        o.mode == "mon" -> oppMon?.types ?: emptyList()
        else -> listOf(o.t1, o.t2).filter { it.isNotEmpty() }
    }
    fun setOpp(change: (Opponent) -> Opponent) = model.update { it.copy(opp = change(it.opp)) }
    val ranked = remember(st.team, st.mode, st.tms, oppTypes, oppMon) {
        val members = Engine.resolveTeam()
        if (oppTypes.isNotEmpty() && members.isNotEmpty()) Engine.rankVs(members, oppTypes, oppMon) else emptyList()
    }
    val lead = remember(ranked) { if (ranked.isEmpty()) null else leadText(ranked) }

    Page {
        item {
            Card {
                SectionTitle("Contra quem você vai lutar?")
                Segmented(listOf("type" to "Por tipo", "mon" to "Por Pokémon"), o.mode) { k -> setOpp { it.copy(mode = k) } }
                if (o.mode == "type") {
                    Muted("Toque em até 2 tipos (ginásio, líder).", 13f)
                    FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                        for (t in TypeChart.all) TypeButton(t, t == o.t1 || t == o.t2) { setOpp { tapType(it, t) } }
                    }
                } else {
                    if (oppMon != null) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Portrait(oppMon, 62.dp)
                            Spacer(Modifier.width(12.dp))
                            Column(verticalArrangement = Arrangement.spacedBy(3.dp)) {
                                Txt(oppMon.name, 16f, FontWeight.ExtraBold)
                                Chips(oppTypes)
                                Muted("VEL ${oppMon.stats[5]} · BST ${oppMon.bst}", 12.5f)
                            }
                        }
                    }
                    BigButton(if (oppMon == null) "Escolher Pokémon adversário" else "Trocar adversário", primary = oppMon == null) {
                        ui.sheet = MonSheet("Pokémon adversário") { p -> setOpp { it.copy(mon = p.name) } }
                    }
                }
                if (oppTypes.isNotEmpty()) {
                    Column(verticalArrangement = Arrangement.spacedBy(5.dp)) {
                        Label("Tipos que acertam em cheio")
                        Chips(TypeChart.all.filter { a -> oppTypes.fold(1.0) { m, t -> m * TypeChart.eff(a, t) } >= 2 })
                    }
                }
            }
        }
        item { SectionTitle("Quem mandar", "Ordenado por dano causado ÷ dano recebido") }
        if (ranked.isEmpty()) {
            item { Card { Muted("Escolha um adversário e tenha pelo menos um Pokémon no time.", 14f) } }
        } else {
            if (lead != null) item { AlertBox(Alert("good", lead)) }
            itemsIndexed(ranked) { i, r -> RankCard(i + 1, r) }
        }
        item {
            val md = st.mode
            val tips = Engine.formatTips.getValue(md.format).toMutableList()
            if (md.pick < md.size) tips.add("Você monta ${md.size} e escolhe ${md.pick} depois de ver o time adversário. Leve quem tem vantagem de tipo contra eles.")
            if (md.level == "50" || md.level == "100") tips.add("Todos lutam no nível ${Engine.battleLv(1)}, então a diferença de nível some e o que conta são atributos, tipos e golpes.")
            if (md.level == "lc") tips.add("Little Cup: só Pokémon no 1º estágio, no nível 5. Atributos de defesa e velocidade pesam muito.")
            if (md.sleepClause) tips.add("Cláusula de sono: golpes que fazem dormir (Spore, Sleep Powder, Hypnosis, Yawn) ficam fora das sugestões.")
            Card(spacing = 8.dp) {
                SectionTitle("Como jogar: " + Engine.formatLabel[md.format], Engine.modeSummary())
                Column(verticalArrangement = Arrangement.spacedBy(5.dp)) {
                    for (t in tips) Rich("•  $t", 13.5f)
                }
            }
        }
    }
}
