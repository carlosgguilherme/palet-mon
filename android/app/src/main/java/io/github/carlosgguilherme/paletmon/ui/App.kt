package io.github.carlosgguilherme.paletmon.ui

import androidx.activity.compose.BackHandler
import androidx.compose.animation.Crossfade
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListScope
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Icon
import androidx.compose.runtime.Composable
import androidx.compose.runtime.Stable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.graphics.vector.addPathNodes
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import io.github.carlosgguilherme.paletmon.AppModel
import io.github.carlosgguilherme.paletmon.logic.Engine

private fun icon(path: String) = ImageVector.Builder(defaultWidth = 24.dp, defaultHeight = 24.dp, viewportWidth = 24f, viewportHeight = 24f)
    .addPath(addPathNodes(path), fill = SolidColor(Color.Black)).build()

// Material icons (Apache 2.0), 24x24.
object Icons {
    val team = icon("M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5s-3 1.34-3 3 1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z")
    val build = icon("M11.99 18.54l-7.37-5.73L3 14.07l9 7 9-7-1.63-1.27-7.38 5.74zM12 16l7.36-5.73L21 9l-9-7-9 7 1.63 1.27L12 16z")
    val spark = icon("M19 9l1.25-2.75L23 5l-2.75-1.25L19 1l-1.25 2.75L15 5l2.75 1.25L19 9zm-7.5.5L9 4 6.5 9.5 1 12l5.5 2.5L9 20l2.5-5.5L17 12l-5.5-2.5zM19 15l-1.25 2.75L15 19l2.75 1.25L19 23l1.25-2.75L23 19l-2.75-1.25L19 15z")
    val bolt = icon("M11 21h-1l1-7H7.5c-.58 0-.57-.32-.38-.66.19-.34.05-.08.07-.12C8.48 10.94 10.42 7.54 13 3h1l-1 7h3.5c.49 0 .56.33.47.51l-.07.15C12.96 17.55 11 21 11 21z")
    val info = icon("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z")
    val down = icon("M7.41 8.59L12 13.17l4.59-4.58L18 10l-6 6-6-6 1.41-1.41z")
    val plus = icon("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z")
}

private class NavItem(val key: String, val label: String, val icon: ImageVector)

private val navItems = listOf(
    NavItem("time", "Meu time", Icons.team), NavItem("montar", "Montar", Icons.build), NavItem("gerador", "Gerador", Icons.spark),
    NavItem("batalha", "Batalha", Icons.bolt), NavItem("sobre", "Sobre", Icons.info),
)

// Screen state that is not saved between sessions.
@Stable
class UiState {
    var sheet by mutableStateOf<Sheet?>(null)
    var section by mutableStateOf("time")
    var expanded by mutableIntStateOf(-1)
    var allAlerts by mutableStateOf(false)
}

@Composable
fun PaletMonApp(model: AppModel = viewModel()) {
    PaletMonTheme {
        if (!model.ready) Splash()
        else {
            val ui = remember { UiState() }
            val modeLabel = remember(model.state.mode) { Engine.modeShort() }
            Column(Modifier.fillMaxSize().background(pm.bg)) {
                TopBar(modeLabel, ui)
                Crossfade(model.state.tab, Modifier.weight(1f), animationSpec = tween(130), label = "tab") { tab ->
                    when (tab) {
                        "montar" -> BuildScreen(model, ui)
                        "gerador" -> GeneratorScreen(model, ui)
                        "batalha" -> BattleScreen(model, ui)
                        "sobre" -> AboutScreen(model)
                        else -> TeamScreen(model, ui)
                    }
                }
                BottomNav(model.state.tab, model::setTab)
            }
            if (model.state.tab != "time") BackHandler { model.setTab("time") }
            Sheets(model, ui)
        }
    }
}

@Composable
private fun Splash() {
    Box(Modifier.fillMaxSize().background(pm.ink), contentAlignment = Alignment.Center) {
        Logo(120.dp)
    }
}

@Composable
private fun TopBar(modeLabel: String, ui: UiState) {
    Column(Modifier.fillMaxWidth().background(pm.ink)) {
        Row(
            Modifier.statusBarsPadding().fillMaxWidth().padding(horizontal = 14.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Logo(40.dp)
            Spacer(Modifier.width(10.dp))
            Wordmark(16f, Color.White)
            Spacer(Modifier.weight(1f))
            Row(
                Modifier.clip(RoundedCornerShape(99.dp)).background(Color.White.copy(alpha = 0.19f))
                    .clickable { ui.sheet = ModeSheet }.padding(start = 12.dp, end = 8.dp, top = 7.dp, bottom = 7.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Txt(modeLabel, 12.5f, FontWeight.Bold, Color.White, maxLines = 1)
                Icon(Icons.down, null, Modifier.size(18.dp), tint = Color.White)
            }
        }
        HazardStripe()
    }
}

@Composable
private fun BottomNav(tab: String, go: (String) -> Unit) {
    Row(
        Modifier.fillMaxWidth().shadow(8.dp).background(pm.panel).navigationBarsPadding().padding(horizontal = 4.dp, vertical = 8.dp),
    ) {
        for (item in navItems) {
            val on = item.key == tab
            val pill by animateColorAsState(if (on) pm.accent else Color.Transparent, tween(130), label = "nav")
            Column(
                Modifier.weight(1f).clickable(remember { MutableInteractionSource() }, indication = null) { go(item.key) },
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(3.dp),
            ) {
                Box(Modifier.size(58.dp, 30.dp).clip(RoundedCornerShape(15.dp)).background(pill), contentAlignment = Alignment.Center) {
                    Icon(item.icon, null, Modifier.size(22.dp), tint = if (on) pm.accentFg else pm.muted)
                }
                Txt(item.label, 11.5f, if (on) FontWeight.ExtraBold else FontWeight.SemiBold, if (on) pm.fg else pm.muted, maxLines = 1)
            }
        }
    }
}

// Scrolling page body shared by every tab.
@Composable
fun Page(state: LazyListState = rememberLazyListState(), content: LazyListScope.() -> Unit) {
    LazyColumn(
        Modifier.fillMaxSize(),
        state = state,
        contentPadding = PaddingValues(start = 14.dp, end = 14.dp, top = 14.dp, bottom = 24.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
        content = content,
    )
}

@Composable
fun Gap(height: Int) = Spacer(Modifier.height(height.dp))
