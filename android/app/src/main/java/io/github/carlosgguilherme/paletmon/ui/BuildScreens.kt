package io.github.carlosgguilherme.paletmon.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import io.github.carlosgguilherme.paletmon.AppModel
import io.github.carlosgguilherme.paletmon.data.Dex
import io.github.carlosgguilherme.paletmon.logic.Alert
import io.github.carlosgguilherme.paletmon.logic.BoxConfig
import io.github.carlosgguilherme.paletmon.logic.Engine
import io.github.carlosgguilherme.paletmon.logic.GenResult
import io.github.carlosgguilherme.paletmon.logic.TypeChart
import kotlin.math.roundToInt

// Checkbox list shared by Gerador and Montar.
@Composable
private fun BoxList(cfg: BoxConfig, keys: List<String>, ui: UiState, changed: (BoxConfig) -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(2.dp)) {
        for (box in Engine.boxes.filter { it.key in keys }) {
            ToggleRow(box.title, box.desc, cfg[box.key]) { v ->
                var next = cfg.with(box.key, v)
                if (box.key == "wishOnly" && v) next = next.with("wishPrio", false)
                changed(next)
            }
            if (!cfg[box.key]) continue
            when (box.extra) {
                "weatherKind" -> SelectRow("Clima", Engine.weathers.getValue(cfg.weatherKind).label) {
                    ui.sheet = PickSheet("Clima", Engine.weathers.map { it.key to it.value.label }, cfg.weatherKind) { k -> changed(cfg.copy(weatherKind = k)) }
                }
                "monoType" -> SelectRow("Tipo", TypeChart.pt.getValue(cfg.monoType)) {
                    ui.sheet = PickSheet("Tipo", TypeChart.all.map { it to TypeChart.pt.getValue(it) }, cfg.monoType) { k -> changed(cfg.copy(monoType = k)) }
                }
                "genNum" -> SelectRow("Geração", "Geração ${cfg.genNum}") {
                    ui.sheet = PickSheet("Geração", (1..9).map { "$it" to "Geração $it" }, "${cfg.genNum}") { k -> changed(cfg.copy(genNum = k.toInt())) }
                }
            }
        }
        if (cfg["fast"] && cfg["trickroom"]) AlertBox(Alert("warn", "Rápido e Trick Room puxam em direções opostas. O resultado fica menos focado."))
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun ResultCard(
    title: String,
    r: GenResult,
    best: Boolean,
    cfg: BoxConfig,
    tms: Boolean,
    reasons: Boolean,
    buttons: @Composable () -> Unit,
) {
    val cores = remember(r) { r.team.filter { it.core } }
    val why = remember(r, cfg) { Engine.explainTeam(r.team, cfg) }
    val moves = remember(r, tms) { r.team.map { z -> Engine.pickMoves(z.p, null, tms).joinToString(" · ") { it.name } } }
    val evs = remember(r) { r.team.map { z -> Engine.buildOf(z.p).let { "EVs: **${Engine.evText(it)}** · ${it.nature}" } } }
    val sc = r.sc
    Card(pad = 14.dp, highlight = best) {
        Heading(title, 16f)
        FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Pill("STAB cobre ${sc.covered.size}/18", if (sc.covered.size >= 14) "good" else "warn")
            Pill(
                if (sc.holes.isNotEmpty()) "Brechas: " + sc.holes.joinToString(", ") { TypeChart.pt.getValue(it) } else "Sem brechas",
                if (sc.holes.isNotEmpty()) "warn" else "good",
            )
            Pill("BST médio ${sc.avgBst.roundToInt()}", "neutral")
        }
        r.team.forEachIndexed { i, z ->
            Row(Modifier.padding(vertical = 6.dp)) {
                Portrait(z.p, 62.dp)
                Spacer(Modifier.width(12.dp))
                Column(verticalArrangement = Arrangement.spacedBy(3.dp)) {
                    Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically) {
                        Txt(z.p.name, 15.5f, FontWeight.ExtraBold)
                        if (z.core) Pill("escolhido", "good") else if (z.kept) Pill("seu", "neutral")
                    }
                    Chips(z.p.types)
                    Rich("Habilidade: **${z.ab}**", 13f)
                    Muted("${Engine.roleOf(z.p).name} · BST ${z.p.bst}", 12.5f)
                    Rich(evs[i], 12.5f)
                    if (reasons && !z.core && cores.isNotEmpty()) Txt(Engine.supportReason(z, cores), 12.5f)
                    Muted(moves[i], 12.5f)
                }
            }
        }
        if (why.isNotEmpty()) {
            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                for (w in why) Rich("•  $w", 13.5f)
            }
        }
        buttons()
    }
}

@Composable
fun BuildScreen(model: AppModel, ui: UiState) {
    val st = model.state
    val full = st.wish.size >= Engine.maxCore
    val results = model.buildResults
    val list = rememberLazyListState()
    // jumps to the first option when a build finishes, not when coming back to this tab
    val seen = remember { mutableStateOf(results) }
    LaunchedEffect(results) {
        if (results !== seen.value) {
            seen.value = results
            if (!results.isNullOrEmpty()) list.animateScrollToItem(2)
        }
    }

    Page(list) {
        item {
            Card(spacing = 10.dp) {
                SectionTitle("1. Escolha seus Pokémon", "${st.wish.size}/${Engine.maxCore} escolhidos · o resto é montado em volta deles")
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    for (id in st.wish) {
                        val p = Dex.byId[id] ?: continue
                        val fin = Engine.finalForms(p).filter { it.id != p.id }.map { it.name }
                        Row(
                            Modifier.fillMaxWidth().clip(RoundedCornerShape(14.dp)).background(pm.accentSoft).padding(8.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Portrait(p, 46.dp)
                            Spacer(Modifier.width(12.dp))
                            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                                Txt(p.name, 15f, FontWeight.ExtraBold)
                                if (fin.isNotEmpty() && st.finals && st.mode.level != "lc") Muted("→ " + fin.joinToString(" / "), 12.5f)
                            }
                            Box(
                                Modifier.size(40.dp).clip(RoundedCornerShape(10.dp)).clickable { model.wishChanged { w -> w - id } },
                                contentAlignment = Alignment.Center,
                            ) { Txt("×", 22f, FontWeight.Bold, pm.muted) }
                        }
                    }
                    if (st.wish.isEmpty()) Muted("Nenhum Pokémon escolhido. Adicione de 1 a ${Engine.maxCore} Pokémon que você quer usar de qualquer jeito.", 14f)
                }
                BigButton(if (full) "Limite atingido" else "Adicionar Pokémon", enabled = !full) {
                    ui.sheet = MonSheet("Escolha um Pokémon") { p ->
                        model.wishChanged { w -> if (p.id !in w && w.size < Engine.maxCore) w + p.id else w }
                    }
                }
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    BigButton("Usar meu time", primary = false, modifier = Modifier.weight(1f)) {
                        model.wishChanged { w ->
                            var next = w
                            for (m in model.state.team) if (m.id !in next && next.size < Engine.maxCore) next = next + m.id
                            next
                        }
                    }
                    BigButton("Limpar", primary = false, modifier = Modifier.weight(1f)) { model.wishChanged { emptyList() } }
                }
                ToggleRow("Planejar com as evoluções finais", on = st.finals) { v ->
                    model.update { it.copy(finals = v) }
                    model.buildResults = null
                }
            }
        }
        item {
            Card(spacing = 8.dp) {
                SectionTitle("2. Como completar o time", "Marque o estilo dos Pokémon que vão entrar junto")
                BoxList(st.build, Engine.buildKeys, ui) { c ->
                    model.update { it.copy(build = c) }
                    model.buildChanged()
                }
                BigButton(if (model.building) "Montando…" else "Montar time em volta deles", enabled = st.wish.isNotEmpty() && !model.building) { model.build(fresh = true) }
                Muted("Mostra até 3 opções diferentes.", 12.5f, Modifier.fillMaxWidth(), TextAlign.Center)
            }
        }
        if (results != null) {
            if (results.isEmpty()) {
                item { AlertBox(Alert("warn", "Nenhum Pokémon atende às caixas marcadas. Desmarque algum filtro (Monotipo, Uma geração ou Sem tipos repetidos).")) }
            }
            itemsIndexed(results) { i, r ->
                ResultCard(if (i == 0) "Opção 1 · melhor encaixe" else "Opção ${i + 1}", r, i == 0, st.build, st.tms, reasons = true) {
                    BigButton("Usar como meu time") { useAsTeam(model, ui, r) }
                }
            }
            if (results.isNotEmpty()) {
                item { BigButton(if (model.building) "Montando…" else "Ver outras opções", enabled = !model.building, primary = false) { model.build(fresh = false) } }
            }
        }
    }
}

private fun useAsTeam(model: AppModel, ui: UiState, r: GenResult) {
    ui.expanded = -1
    ui.section = "time"
    model.useAsTeam(r.team)
}

@Composable
fun GeneratorScreen(model: AppModel, ui: UiState) {
    val st = model.state
    val r = model.genResult
    var addedFor by remember { mutableStateOf<GenResult?>(null) }

    Page {
        item {
            val label = if (model.generating) "Gerando…" else if (r == null) "Gerar time" else "Gerar outro"
            BigButton(label, enabled = !model.generating) { model.generate() }
        }
        if (model.genEmpty) {
            item { AlertBox(Alert("warn", "Nenhum Pokémon atende a todas as caixas marcadas. Desmarque algum filtro (Monotipo, Uma geração ou Só da minha lista).")) }
        } else if (r != null) {
            item {
                ResultCard("Time gerado · ${r.poolSize} possíveis", r, true, st.gen, st.tms, reasons = false) {
                    BigButton("Usar como meu time") { useAsTeam(model, ui, r) }
                    val added = addedFor === r
                    BigButton(if (added) "Na lista (${st.wish.size}/${Engine.maxCore})" else "Adicionar à minha lista", enabled = !added, primary = false) {
                        model.wishChanged { w ->
                            var next = w
                            for (z in r.team) {
                                if (next.size < Engine.maxCore && next.none { Dex.byId[it]?.chain == z.p.chain }) next = next + z.p.id
                            }
                            next
                        }
                        addedFor = r
                    }
                }
            }
        }
        item {
            Card(spacing = 6.dp) {
                SectionTitle("Como você quer o time", "Dá para combinar várias opções")
                BoxList(st.gen, Engine.boxes.map { it.key }, ui) { c ->
                    model.update { it.copy(gen = c) }
                    model.genChanged()
                }
            }
        }
    }
}
