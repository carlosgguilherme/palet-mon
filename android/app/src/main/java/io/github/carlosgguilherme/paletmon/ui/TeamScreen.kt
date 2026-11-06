package io.github.carlosgguilherme.paletmon.ui

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.RowScope
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyListScope
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Icon
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import io.github.carlosgguilherme.paletmon.AppModel
import io.github.carlosgguilherme.paletmon.logic.Alert
import io.github.carlosgguilherme.paletmon.logic.Coverage
import io.github.carlosgguilherme.paletmon.logic.DefRow
import io.github.carlosgguilherme.paletmon.logic.Engine
import io.github.carlosgguilherme.paletmon.logic.Member
import io.github.carlosgguilherme.paletmon.logic.Suggestion
import io.github.carlosgguilherme.paletmon.logic.TeamScore
import io.github.carlosgguilherme.paletmon.logic.TeamSlot
import io.github.carlosgguilherme.paletmon.logic.TypeChart
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlin.math.roundToInt

private class TeamData(val members: List<Member>, val sc: TeamScore, val offense: List<Coverage>, val alerts: List<Alert>)

private fun teamData(tms: Boolean): TeamData {
    val members = Engine.resolveTeam()
    for (x in members) x.moves = Engine.pickMoves(x.p, Engine.battleLv(x.lv), tms)
    val sc = Engine.scoreTeam(members)
    return TeamData(members, sc, Engine.offense(members), Engine.teamAlerts(members, sc))
}

@Composable
fun TeamScreen(model: AppModel, ui: UiState) {
    val st = model.state
    val d = remember(st.team, st.mode, st.tms) { teamData(st.tms) }
    val covered = d.offense.count { it.best >= 2 }
    val holes = d.sc.holes.size

    Page {
        item {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Tile("$covered/18", "cobertura", if (covered >= 14) "good" else if (covered >= 10) "warn" else "bad", Modifier.weight(1f))
                Tile("$holes", if (holes == 1) "brecha" else "brechas", if (holes == 0) "good" else if (holes <= 2) "warn" else "bad", Modifier.weight(1f))
                Tile("${d.sc.avgBst.roundToInt()}", "BST médio", "neutral", Modifier.weight(1f))
            }
        }
        item { Segmented(listOf("time" to "Time", "defesa" to "Defesa", "ataque" to "Ataque"), ui.section) { ui.section = it } }
        when (ui.section) {
            "defesa" -> item { DefenseSection(d.members, d.sc) }
            "ataque" -> attackSection(d, model)
            else -> teamSection(d, model, ui)
        }
    }
}

private fun LazyListScope.teamSection(d: TeamData, model: AppModel, ui: UiState) {
    val shown = if (ui.allAlerts) d.alerts else d.alerts.take(2)
    items(shown) { AlertBox(it) }
    if (d.alerts.size > 2) {
        item {
            val more = d.alerts.size - 2
            val label = if (ui.allAlerts) "Mostrar menos avisos" else if (more == 1) "Ver mais 1 aviso" else "Ver mais $more avisos"
            Box(Modifier.fillMaxWidth().clickable { ui.allAlerts = !ui.allAlerts }.padding(vertical = 2.dp), contentAlignment = Alignment.Center) {
                Txt(label, 13.5f, FontWeight.Bold, pm.accentInk)
            }
        }
    }
    itemsIndexed(d.members) { i, x -> MonCard(x, i, model, ui) }
    if (d.members.size < 6) {
        item {
            val shape = RoundedCornerShape(18.dp)
            Row(
                Modifier.fillMaxWidth().clip(shape).border(1.5.dp, pm.accentInk, shape).clickable {
                    ui.sheet = MonSheet("Adicionar ao time") { p ->
                        model.update { st -> st.copy(team = st.team + TeamSlot(p.id, st.team.maxOfOrNull { it.lv } ?: 5)) }
                        ui.expanded = model.state.team.size - 1
                    }
                }.padding(14.dp),
                horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically,
            ) {
                Icon(Icons.plus, null, Modifier.size(22.dp), tint = pm.accentInk)
                Spacer(Modifier.width(8.dp))
                Txt("Adicionar Pokémon (${d.members.size}/6)", 15f, FontWeight.Bold, pm.accentInk)
            }
        }
    }
    item {
        Card(pad = 10.dp) {
            ToggleRow("Golpes de TM, tutor e ovo", "Considerar nas sugestões de golpes", model.state.tms) { v -> model.update { it.copy(tms = v) } }
        }
    }
}

@Composable
private fun MonCard(x: Member, i: Int, model: AppModel, ui: UiState) {
    val open = ui.expanded == i
    val ready = remember(x.p.id, x.lv) { Engine.nextEvos(x.p).any { it.evo != null && it.evo.level > 0 && x.lv >= it.evo.level } }
    Card(pad = 0.dp, spacing = 0.dp) {
        Row(
            Modifier.fillMaxWidth().background(typeBrush(x.p.types)).clickable { ui.expanded = if (open) -1 else i }
                .padding(start = 16.dp, end = 10.dp, top = 10.dp, bottom = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Txt(x.p.name, 19f, color = Color.White, family = Bungee, maxLines = 1)
                Row(horizontalArrangement = Arrangement.spacedBy(4.dp)) { for (t in x.p.types) GlassChip(TypeChart.pt.getValue(t)) }
                Row(horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                    GlassChip("Nv ${Engine.battleLv(x.lv)}")
                    if (ready) GlassChip("Pronto para evoluir")
                }
            }
            Box(Modifier.size(96.dp), contentAlignment = Alignment.Center) {
                Box(Modifier.size(92.dp).clip(CircleShape).background(Color.White.copy(alpha = 0.17f)))
                MonImage(x.p, Modifier.size(90.dp), art = true, initialsSize = 30f)
            }
        }
        AnimatedVisibility(open) {
            Box(Modifier.padding(14.dp)) { MonDetails(x, i, model, ui) }
        }
    }
}

@Composable
private fun MonDetails(x: Member, i: Int, model: AppModel, ui: UiState) {
    fun slot(change: (TeamSlot) -> TeamSlot) = model.update { st -> st.copy(team = st.team.mapIndexed { j, s -> if (j == i) change(s) else s }) }
    val role = Engine.roleOf(x.p)
    val abilities = x.p.abilities.map { a -> a.trimStart('*') to a.trimStart('*') + if (a.startsWith("*")) " (oculta)" else "" }
    val moveLabel = "Golpes sugeridos no nível ${Engine.battleLv(x.lv)}" + if (Engine.isMulti) " · ${Engine.formatLabel[model.state.mode.format]?.lowercase()}" else ""

    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Label("Nível")
            Stepper(x.lv) { v -> slot { it.copy(lv = v) } }
        }
        SelectRow("Habilidade", x.ab) {
            ui.sheet = PickSheet("Habilidade", abilities, x.ab) { k -> slot { it.copy(ab = k) } }
        }
        Column(verticalArrangement = Arrangement.spacedBy(3.dp)) {
            Rich("**${role.name}**", 15f)
            Muted(role.tip, 13f)
        }
        StatBars(x.p)
        EvoBlock(x.p, x.lv)
        Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Label(moveLabel)
            if (x.moves.isEmpty()) Muted("Sem golpes de dano disponíveis ainda.")
            for (pair in x.moves.chunked(2)) {
                Row(Modifier.height(IntrinsicSize.Min), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    for (mv in pair) MoveCard(mv, Modifier.weight(1f).fillMaxHeight())
                    if (pair.size == 1) Spacer(Modifier.weight(1f))
                }
            }
        }
        BigButton("Remover do time", primary = false) {
            ui.expanded = -1
            model.update { st -> st.copy(team = st.team.filterIndexed { j, _ -> j != i }) }
        }
    }
}

@Composable
private fun RowScope.Cell(text: String, bg: Color?, fg: Color, bold: Boolean = true, family: FontFamily? = null) {
    var m = Modifier.weight(1f).clip(RoundedCornerShape(5.dp))
    if (bg != null) m = m.background(bg)
    Box(m.padding(vertical = 2.dp), contentAlignment = Alignment.Center) {
        Txt(text, 12.5f, if (bold) FontWeight.Bold else FontWeight.Normal, fg, family = family, maxLines = 1, align = TextAlign.Center)
    }
}

@Composable
private fun RowScope.MultCell(v: Double) {
    val (bg, fg) = when {
        v == 0.0 -> pm.immBg to pm.fg
        v >= 4 -> pm.bad to pm.panel
        v >= 2 -> pm.badBg to pm.bad
        v <= 0.25 -> pm.good to pm.panel
        v < 1 -> pm.goodBg to pm.good
        else -> null to pm.muted
    }
    Cell(TypeChart.mult(v), bg, fg, bold = v != 1.0)
}

@Composable
private fun DefenseRow(row: DefRow, n: Int) {
    Row(horizontalArrangement = Arrangement.spacedBy(3.dp), verticalAlignment = Alignment.CenterVertically) {
        Box(Modifier.width(76.dp)) { Chip(row.atk) }
        for (i in 0 until n) MultCell(row.mults[i])
        Cell("${row.weak}", if (row.weak >= 3) pm.badBg else null, if (row.weak >= 3) pm.bad else pm.muted)
        Cell("${row.res}", if (row.res > 0) pm.goodBg else null, if (row.res > 0) pm.good else pm.muted)
    }
}

@Composable
private fun DefenseSection(members: List<Member>, sc: TeamScore) {
    Card(pad = 12.dp, spacing = 8.dp) {
        if (members.isEmpty()) {
            Muted("Adicione Pokémon para ver a tabela.", 14f)
            return@Card
        }
        SectionTitle("Defesa", "Dano recebido por tipo de golpe")
        Column(verticalArrangement = Arrangement.spacedBy(3.dp)) {
            Row(horizontalArrangement = Arrangement.spacedBy(3.dp), verticalAlignment = Alignment.CenterVertically) {
                Spacer(Modifier.width(76.dp))
                for (x in members) Box(Modifier.weight(1f), contentAlignment = Alignment.Center) { Portrait(x.p, 26.dp, art = false) }
                Cell("Fr", null, pm.muted)
                Cell("Res", null, pm.muted)
            }
            for (row in sc.table) DefenseRow(row, members.size)
        }
        Muted("Fr: quantos do time são fracos. Res: quantos resistem.", 12f)
    }
}

private fun LazyListScope.attackSection(d: TeamData, model: AppModel) {
    item {
        Card(pad = 12.dp, spacing = 8.dp) {
            SectionTitle("Ataque", "Melhor dano com os golpes sugeridos")
            Column(verticalArrangement = Arrangement.spacedBy(5.dp)) {
                for (pair in d.offense.chunked(2)) {
                    Row(horizontalArrangement = Arrangement.spacedBy(5.dp)) {
                        for (o in pair) CoverageCell(o, Modifier.weight(1f))
                    }
                }
            }
            Muted("Verde: alguém do time acerta super efetivo. Vermelho: o melhor golpe ainda é pouco efetivo.", 12.5f)
        }
    }
    item { SectionTitle("Quem completaria o time") }
    item { SuggestionList(d.members, model) }
}

@Composable
private fun CoverageCell(o: Coverage, modifier: Modifier) {
    val bg = if (o.best >= 2) pm.goodBg else if (o.best < 1) pm.badBg else pm.panel2
    val who = if (o.best >= 2) o.who[0] else "×" + TypeChart.mult(o.best)
    Row(
        modifier.clip(RoundedCornerShape(9.dp)).background(bg).padding(start = 5.dp, end = 8.dp, top = 5.dp, bottom = 5.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Chip(o.type)
        Spacer(Modifier.width(6.dp))
        Txt(who, 11.5f, color = pm.muted, modifier = Modifier.weight(1f), maxLines = 1, align = TextAlign.End)
    }
}

// Scans every Pokémon, so it runs off the UI thread.
@Composable
private fun SuggestionList(members: List<Member>, model: AppModel) {
    val list by produceState<List<Suggestion>?>(null, members) {
        value = withContext(Dispatchers.Default) { Engine.suggestions(members, 6) }
    }
    val found = list
    if (found == null) {
        Card { Muted("Procurando…", 14f) }
        return
    }
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        for (s in found) {
            val chosen = s.p.id in model.state.wish
            Card(pad = 12.dp, spacing = 8.dp) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Portrait(s.p, 58.dp)
                    Spacer(Modifier.width(12.dp))
                    Column(verticalArrangement = Arrangement.spacedBy(3.dp)) {
                        Txt(s.p.name, 15.5f, FontWeight.ExtraBold)
                        Chips(s.p.types)
                    }
                }
                Muted(s.why)
                BigButton(if (chosen) "Escolhido" else "Escolher para Montar time", enabled = !chosen, primary = false) {
                    model.wishChanged { w -> if (s.p.id !in w && w.size < Engine.maxCore) w + s.p.id else w }
                }
            }
        }
    }
}
