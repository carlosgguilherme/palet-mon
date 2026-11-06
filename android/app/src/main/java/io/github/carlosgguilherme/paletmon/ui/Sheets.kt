package io.github.carlosgguilherme.paletmon.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.carlosgguilherme.paletmon.AppModel
import io.github.carlosgguilherme.paletmon.UpdateNotice
import io.github.carlosgguilherme.paletmon.data.Dex
import io.github.carlosgguilherme.paletmon.data.Mon
import io.github.carlosgguilherme.paletmon.logic.BattleMode
import io.github.carlosgguilherme.paletmon.logic.Engine
import kotlinx.coroutines.launch

sealed interface Sheet
data object ModeSheet : Sheet
class PickSheet(val title: String, val options: List<Pair<String, String>>, val selected: String, val back: Sheet? = null, val pick: (String) -> Unit) : Sheet
class MonSheet(val title: String, val pick: (Mon) -> Unit) : Sheet

// Slides the sheet away, then runs what comes next.
private typealias Hide = (after: () -> Unit) -> Unit

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun SheetFrame(
    title: String,
    locked: Boolean = false,
    tall: Boolean = false,
    dismissed: () -> Unit,
    content: @Composable ColumnScope.(hide: Hide) -> Unit,
) {
    val isLocked by rememberUpdatedState(locked)
    val state = rememberModalBottomSheetState(skipPartiallyExpanded = true, confirmValueChange = { !isLocked })
    val scope = rememberCoroutineScope()
    val hide: Hide = { after ->
        scope.launch { state.hide() }.invokeOnCompletion { after() }
    }
    ModalBottomSheet(
        onDismissRequest = dismissed,
        sheetState = state,
        containerColor = pm.panel,
        contentColor = pm.fg,
        shape = RoundedCornerShape(topStart = 22.dp, topEnd = 22.dp),
        dragHandle = {
            Box(Modifier.padding(top = 10.dp).width(42.dp).height(4.dp).clip(RoundedCornerShape(2.dp)).background(pm.line))
        },
    ) {
        var m = Modifier.fillMaxWidth().padding(horizontal = 16.dp).navigationBarsPadding().padding(bottom = 18.dp)
        if (tall) m = m.fillMaxHeight(0.86f)
        Column(m) {
            Gap(12)
            Heading(title)
            Gap(10)
            content(hide)
        }
    }
}

@Composable
fun Sheets(model: AppModel, ui: UiState) {
    val notice = model.notice
    if (notice != null) {
        // its own frame, so a sheet that was open underneath comes back intact
        key("update") {
            val locked = notice.actions.isEmpty()
            SheetFrame("Atualização", locked = locked, dismissed = { model.notice = null }) { hide -> UpdateBody(notice, model, hide) }
        }
        return
    }
    val sheet = ui.sheet ?: return
    val title = when (sheet) {
        is ModeSheet -> "Modo de combate"
        is PickSheet -> sheet.title
        is MonSheet -> sheet.title
    }
    // one frame for all of them: moving from the mode sheet to a list swaps the content in place
    SheetFrame(title, tall = sheet is MonSheet, dismissed = { ui.sheet = null }) { hide ->
        when (sheet) {
            is ModeSheet -> ModeBody(model, ui, hide)
            is PickSheet -> PickBody(sheet, ui, hide)
            is MonSheet -> key(sheet) { MonBody(sheet, ui, hide) }
        }
    }
}

@Composable
private fun UpdateBody(n: UpdateNotice, model: AppModel, hide: Hide) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Rich(n.text, 15f)
        if (n.progress) {
            LinearProgressIndicator(
                progress = { model.progress / 100f },
                modifier = Modifier.fillMaxWidth().height(8.dp).clip(RoundedCornerShape(4.dp)),
                color = pm.accent, trackColor = pm.panel2, drawStopIndicator = {},
            )
        }
        for (a in n.actions) {
            BigButton(a.label, primary = a.primary) { if (a.primary) a.run() else hide(a.run) }
        }
    }
}

@Composable
private fun PickBody(s: PickSheet, ui: UiState, hide: Hide) {
    Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(2.dp)) {
        for ((key, label) in s.options) {
            val on = key == s.selected
            var row = Modifier.fillMaxWidth().clip(RoundedCornerShape(12.dp))
            if (on) row = row.background(pm.accentSoft)
            Box(
                row.clickable {
                    s.pick(key)
                    if (s.back != null) ui.sheet = s.back else hide { ui.sheet = null }
                }.padding(horizontal = 14.dp, vertical = 12.dp),
            ) {
                Txt(label, 15f, if (on) FontWeight.ExtraBold else FontWeight.Normal, if (on) pm.accentInk else pm.fg)
            }
        }
    }
}

@Composable
private fun ColumnScope.MonBody(s: MonSheet, ui: UiState, hide: Hide) {
    var query by remember { mutableStateOf("") }
    val found = remember(query) {
        val q = query.trim()
        Dex.all.filter { it.name.contains(q, ignoreCase = true) }
            .sortedBy { if (it.name.startsWith(q, ignoreCase = true)) 0 else 1 }
    }
    Box(
        Modifier.fillMaxWidth().height(46.dp).clip(RoundedCornerShape(12.dp)).background(pm.panel2).padding(horizontal = 14.dp),
        contentAlignment = Alignment.CenterStart,
    ) {
        if (query.isEmpty()) Muted("Nome do Pokémon", 15f)
        BasicTextField(
            query, { query = it },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            textStyle = TextStyle(color = pm.fg, fontSize = 15.sp),
            cursorBrush = SolidColor(pm.accentInk),
        )
    }
    Gap(10)
    LazyColumn(Modifier.weight(1f).imePadding()) {
        if (found.isEmpty()) item { Muted("Nenhum Pokémon com esse nome.", 14f) }
        items(found, key = { it.id }) { m ->
            Row(
                Modifier.fillMaxWidth().clip(RoundedCornerShape(12.dp))
                    .clickable { hide { ui.sheet = null; s.pick(m) } }.padding(horizontal = 4.dp, vertical = 7.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Portrait(m, 46.dp, art = false)
                Spacer(Modifier.width(12.dp))
                Column(verticalArrangement = Arrangement.spacedBy(3.dp)) {
                    Txt(m.name, 15f, FontWeight.Bold, maxLines = 1)
                    Chips(m.types)
                }
            }
        }
    }
}

@Composable
private fun ModeBody(model: AppModel, ui: UiState, hide: Hide) {
    val m = model.state.mode
    fun custom(change: (BattleMode) -> BattleMode) = model.setMode(change(model.state.mode).copy(preset = "custom"))
    val presets = Engine.presets.map { it.key to it.value.label } + ("custom" to "Personalizado")
    val sizes = listOf(6, 5, 4, 3, 2, 1)
    val picks = sizes.filter { it <= m.size }.map { n -> n.toString() to if (n == m.size) "Todos ($n)" else "$n de ${m.size}" }

    Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        Muted(Engine.modeSummary(), 13f)
        SelectRow("Regra pronta", presets.first { it.first == m.preset }.second) {
            ui.sheet = PickSheet("Regra pronta", presets, m.preset, ModeSheet) { k ->
                if (k == "custom") custom { it } else model.setMode(Engine.presetMode(k))
            }
        }
        Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
            Label("Formato")
            Segmented(Engine.formatLabel.map { it.key to it.value }, m.format) { k -> custom { it.copy(format = k) } }
        }
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            SelectRow("No time", m.size.toString(), Modifier.weight(1f)) {
                ui.sheet = PickSheet("Pokémon no time", sizes.map { it.toString() to it.toString() }, m.size.toString(), ModeSheet) { k ->
                    custom { c ->
                        val all = c.pick == c.size
                        val size = k.toInt()
                        c.copy(size = size, pick = if (all) size else c.pick)
                    }
                }
            }
            SelectRow("Leva", picks.first { it.first == m.pick.toString() }.second, Modifier.weight(1f)) {
                ui.sheet = PickSheet("Leva para a luta", picks, m.pick.toString(), ModeSheet) { k -> custom { it.copy(pick = k.toInt()) } }
            }
        }
        SelectRow("Nível", Engine.levelLabel.getValue(m.level)) {
            ui.sheet = PickSheet("Nível", Engine.levelLabel.map { it.key to it.value }, m.level, ModeSheet) { k -> custom { it.copy(level = k) } }
        }
        ToggleRow("Proibir lendários e míticos", on = m.banLegends) { v -> custom { it.copy(banLegends = v) } }
        ToggleRow("Cláusula de sono", "Tira Spore, Hypnosis e parecidos das sugestões", m.sleepClause) { v -> custom { it.copy(sleepClause = v) } }
        BigButton("Pronto") { hide { ui.sheet = null } }
    }
}
