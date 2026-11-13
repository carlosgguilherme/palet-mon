package io.github.carlosgguilherme.paletmon.ui

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil.compose.AsyncImage
import coil.request.ImageRequest
import io.github.carlosgguilherme.paletmon.data.Mon
import io.github.carlosgguilherme.paletmon.logic.Alert
import io.github.carlosgguilherme.paletmon.logic.Engine
import io.github.carlosgguilherme.paletmon.logic.PMove
import io.github.carlosgguilherme.paletmon.logic.TypeChart

private val CardShape = RoundedCornerShape(18.dp)

fun typeColor(type: String) = Color(TypeChart.color.getValue(type))

private fun Color.shade(f: Float) = Color(red * f, green * f, blue * f, alpha)

fun typeBrush(types: List<String>): Brush {
    val a = typeColor(types[0]).shade(0.86f)
    val b = typeColor(types.getOrElse(1) { types[0] }).shade(0.62f)
    return Brush.linearGradient(listOf(a, b))
}

@Composable
fun Txt(
    text: String,
    size: Float = 14f,
    weight: FontWeight = FontWeight.Normal,
    color: Color = pm.fg,
    modifier: Modifier = Modifier,
    family: FontFamily? = null,
    maxLines: Int = Int.MAX_VALUE,
    align: TextAlign? = null,
) {
    Text(
        text, modifier = modifier, color = color, fontSize = size.sp, fontWeight = weight, fontFamily = family, lineHeight = (size * 1.32f).sp,
        maxLines = maxLines, overflow = if (maxLines == Int.MAX_VALUE) TextOverflow.Clip else TextOverflow.Ellipsis, textAlign = align,
    )
}

@Composable
fun Muted(text: String, size: Float = 13f, modifier: Modifier = Modifier, align: TextAlign? = null) = Txt(text, size, color = pm.muted, modifier = modifier, align = align)

@Composable
fun Label(text: String) = Txt(text.uppercase(), 11f, FontWeight.ExtraBold, pm.muted)

@Composable
fun Heading(text: String, size: Float = 17f) = Txt(text, size, family = Bungee)

// **bold** markup
@Composable
fun Rich(markup: String, size: Float = 14f, color: Color = pm.fg, modifier: Modifier = Modifier) {
    val text = remember(markup) {
        buildAnnotatedString {
            markup.split("**").forEachIndexed { i, part ->
                if (i % 2 == 1) withStyle(SpanStyle(fontWeight = FontWeight.Bold)) { append(part) } else append(part)
            }
        }
    }
    Text(text, modifier = modifier, color = color, fontSize = size.sp, lineHeight = (size * 1.32f).sp)
}

@Composable
fun SectionTitle(text: String, note: String? = null) {
    Column(Modifier.padding(horizontal = 2.dp)) {
        Heading(text)
        if (note != null) Muted(note, 12.5f)
    }
}

@Composable
fun Card(
    modifier: Modifier = Modifier,
    pad: Dp = 16.dp,
    spacing: Dp = 10.dp,
    highlight: Boolean = false,
    content: @Composable ColumnScope.() -> Unit,
) {
    var m = modifier.fillMaxWidth().shadow(2.dp, CardShape, clip = false).clip(CardShape).background(pm.panel)
    if (highlight) m = m.border(2.dp, pm.accent, CardShape)
    Column(m.padding(pad), verticalArrangement = Arrangement.spacedBy(spacing), content = content)
}

@Composable
fun Tile(value: String, label: String, kind: String, modifier: Modifier = Modifier) {
    val (bg, fg) = when (kind) {
        "good" -> pm.goodBg to pm.good
        "warn" -> pm.warnBg to pm.warn
        "bad" -> pm.badBg to pm.bad
        else -> pm.panel to pm.fg
    }
    val shape = RoundedCornerShape(16.dp)
    Column(modifier.shadow(2.dp, shape, clip = false).clip(shape).background(bg).padding(horizontal = 12.dp, vertical = 10.dp)) {
        Txt(value, 22f, color = fg, family = Bungee, maxLines = 1)
        Txt(label, 11.5f, FontWeight.Bold, pm.muted, maxLines = 1)
    }
}

@Composable
fun Segmented(options: List<Pair<String, String>>, selected: String, pick: (String) -> Unit) {
    Row(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(14.dp)).background(pm.panel2).padding(3.dp),
    ) {
        for ((key, label) in options) {
            val on = key == selected
            Box(
                Modifier.weight(1f).clip(RoundedCornerShape(11.dp)).background(if (on) pm.accent else Color.Transparent)
                    .clickable { pick(key) }.padding(vertical = 10.dp),
                contentAlignment = Alignment.Center,
            ) {
                Txt(label, 13f, FontWeight.Bold, if (on) pm.accentFg else pm.muted, maxLines = 1)
            }
        }
    }
}

@Composable
fun Chip(type: String) {
    val fg = if (type in TypeChart.darkText) Color(0xFF1B1B1B) else Color.White
    Box(Modifier.clip(RoundedCornerShape(99.dp)).background(typeColor(type)).padding(horizontal = 8.dp, vertical = 1.dp)) {
        Text(TypeChart.pt.getValue(type).uppercase(), color = fg, fontSize = 11.sp, fontWeight = FontWeight.ExtraBold, letterSpacing = 0.4.sp, maxLines = 1)
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun Chips(types: List<String>) {
    FlowRow(horizontalArrangement = Arrangement.spacedBy(4.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
        for (t in types) Chip(t)
    }
}

@Composable
fun GlassChip(text: String) {
    Box(Modifier.clip(RoundedCornerShape(99.dp)).background(Color.White.copy(alpha = 0.22f)).padding(horizontal = 9.dp, vertical = 2.dp)) {
        Text(text, color = Color.White, fontSize = 11.5.sp, fontWeight = FontWeight.Bold, maxLines = 1)
    }
}

@Composable
fun Pill(text: String, kind: String) {
    val (bg, fg) = when (kind) {
        "good" -> pm.goodBg to pm.good
        "warn" -> pm.warnBg to pm.warn
        "bad" -> pm.badBg to pm.bad
        else -> pm.panel2 to pm.muted
    }
    Box(Modifier.clip(RoundedCornerShape(99.dp)).background(bg).padding(horizontal = 10.dp, vertical = 2.dp)) {
        Txt(text, 12.5f, FontWeight.Bold, fg)
    }
}

@Composable
fun AlertBox(alert: Alert) {
    val (bg, edge) = when (alert.kind) {
        "good" -> pm.goodBg to pm.good
        "bad" -> pm.badBg to pm.bad
        "neutral" -> pm.panel2 to pm.muted
        else -> pm.warnBg to pm.warn
    }
    Box(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(10.dp)).background(bg)
            .drawBehind { drawRect(edge, size = Size(4.dp.toPx(), size.height)) }
            .padding(start = 16.dp, end = 12.dp, top = 9.dp, bottom = 9.dp),
    ) {
        Rich(alert.text, 13.5f)
    }
}

@Composable
fun BigButton(text: String, enabled: Boolean = true, primary: Boolean = true, modifier: Modifier = Modifier, onClick: () -> Unit) {
    val shape = RoundedCornerShape(14.dp)
    val content: @Composable () -> Unit = { Text(text, fontSize = 15.sp, fontWeight = FontWeight.ExtraBold, maxLines = 1, overflow = TextOverflow.Ellipsis) }
    if (primary) {
        Button(
            onClick, modifier.fillMaxWidth().height(50.dp), enabled = enabled, shape = shape,
            colors = ButtonDefaults.buttonColors(containerColor = pm.accent, contentColor = pm.accentFg, disabledContainerColor = pm.panel2, disabledContentColor = pm.muted),
        ) { content() }
    } else {
        OutlinedButton(
            onClick, modifier.fillMaxWidth().height(50.dp), enabled = enabled, shape = shape,
            border = androidx.compose.foundation.BorderStroke(1.5.dp, if (enabled) pm.accentInk else pm.line),
            colors = ButtonDefaults.outlinedButtonColors(contentColor = pm.accentInk, disabledContentColor = pm.muted),
        ) { content() }
    }
}

@Composable
fun ToggleRow(title: String, desc: String? = null, on: Boolean, changed: (Boolean) -> Unit) {
    Row(Modifier.fillMaxWidth().clickable { changed(!on) }.padding(vertical = 8.dp), verticalAlignment = Alignment.CenterVertically) {
        Column(Modifier.weight(1f)) {
            Txt(title, 14.5f, FontWeight.Bold)
            if (!desc.isNullOrEmpty()) Muted(desc, 12.5f)
        }
        Spacer(Modifier.width(12.dp))
        Switch(
            on, onCheckedChange = null,
            colors = SwitchDefaults.colors(checkedTrackColor = pm.accent, checkedThumbColor = Color.White, checkedBorderColor = pm.accent, uncheckedTrackColor = pm.line, uncheckedThumbColor = Color.White, uncheckedBorderColor = pm.line),
        )
    }
}

@Composable
fun SelectRow(label: String, value: String, modifier: Modifier = Modifier, open: () -> Unit) {
    Row(
        modifier.fillMaxWidth().clip(RoundedCornerShape(12.dp)).background(pm.panel2).clickable(onClick = open).padding(horizontal = 14.dp, vertical = 9.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f)) {
            Label(label)
            Txt(value, 14.5f, FontWeight.Bold, maxLines = 1)
        }
        Icon(Icons.down, null, Modifier.size(22.dp), tint = pm.muted)
    }
}

@Composable
fun Stepper(value: Int, changed: (Int) -> Unit) {
    fun clamp(v: Int) = v.coerceIn(1, 100)
    var text by remember(value) { mutableStateOf(value.toString()) }
    val focus = LocalFocusManager.current
    fun commit() {
        val v = text.toIntOrNull()
        if (v != null && clamp(v) != value) changed(clamp(v)) else text = value.toString()
    }
    @Composable
    fun Step(label: String, delta: Int, modifier: Modifier) {
        Box(
            modifier.height(42.dp).clip(RoundedCornerShape(10.dp)).background(pm.panel2).clickable { changed(clamp(value + delta)) },
            contentAlignment = Alignment.Center,
        ) { Txt(label, 14f, FontWeight.ExtraBold) }
    }
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
        Step("−10", -10, Modifier.weight(1f))
        Step("−", -1, Modifier.weight(1f))
        Box(Modifier.weight(1.2f).height(42.dp).border(1.dp, pm.line, RoundedCornerShape(10.dp)), contentAlignment = Alignment.Center) {
            BasicTextField(
                text, { text = it.filter(Char::isDigit).take(3) },
                singleLine = true,
                textStyle = TextStyle(color = pm.fg, fontSize = 16.sp, fontWeight = FontWeight.Bold, textAlign = TextAlign.Center),
                cursorBrush = SolidColor(pm.accentInk),
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number, imeAction = ImeAction.Done),
                keyboardActions = KeyboardActions(onDone = { focus.clearFocus() }),
                modifier = Modifier.fillMaxWidth().onFocusChanged { if (!it.isFocused) commit() },
            )
        }
        Step("+", 1, Modifier.weight(1f))
        Step("+10", 10, Modifier.weight(1f))
    }
}

private const val SPRITES = "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon"

// art: large official artwork. Otherwise the small pixel sprite, light enough for long lists.
@Composable
fun MonImage(mon: Mon, modifier: Modifier = Modifier, art: Boolean = true, initialsSize: Float = 18f) {
    val context = LocalContext.current
    var loaded by remember(mon.id) { mutableStateOf(false) }
    val url = if (art) "$SPRITES/other/official-artwork/${mon.id}.png" else "$SPRITES/${mon.id}.png"
    val request = remember(url) { ImageRequest.Builder(context).data(url).crossfade(120).build() }
    Box(modifier, contentAlignment = Alignment.Center) {
        if (!loaded) {
            val initials = mon.name.split(' ').filter { it.isNotEmpty() }.joinToString("") { it.take(1) }.uppercase().take(2)
            Text(initials, color = Color.White, fontSize = initialsSize.sp, fontFamily = Bungee)
        }
        AsyncImage(
            request, contentDescription = mon.name, modifier = Modifier.fillMaxSize(), contentScale = ContentScale.Fit,
            filterQuality = if (art) FilterQuality.Medium else FilterQuality.None,
            onSuccess = { loaded = true },
        )
    }
}

@Composable
fun Portrait(mon: Mon, size: Dp, art: Boolean = true) {
    Box(Modifier.size(size).clip(CircleShape).background(typeBrush(mon.types))) {
        MonImage(mon, Modifier.fillMaxSize(), art, size.value * 0.36f)
    }
}

@Composable
fun StatBars(mon: Mon) {
    val labels = listOf("HP", "ATQ", "DEF", "AT.E", "DF.E", "VEL")
    Column(verticalArrangement = Arrangement.spacedBy(3.dp)) {
        labels.forEachIndexed { i, label ->
            val v = mon.stats[i]
            val color = when {
                v >= 100 -> pm.good
                v >= 70 -> pm.accent
                v >= 50 -> pm.warn
                else -> pm.bad
            }
            Row(verticalAlignment = Alignment.CenterVertically) {
                Txt(label, 11.5f, FontWeight.ExtraBold, pm.muted, Modifier.width(40.dp))
                Txt(v.toString(), 12f, modifier = Modifier.width(30.dp), align = TextAlign.End)
                Spacer(Modifier.width(8.dp))
                Box(Modifier.weight(1f).height(7.dp).clip(RoundedCornerShape(4.dp)).background(pm.panel2)) {
                    Box(Modifier.fillMaxWidth(minOf(1f, v / 160f)).fillMaxHeight().clip(RoundedCornerShape(4.dp)).background(color))
                }
            }
        }
    }
}

@Composable
fun MoveCard(mv: PMove, modifier: Modifier = Modifier) {
    val c = typeColor(mv.type)
    val cls = when (mv.info.cls) { 2 -> "Físico"; 3 -> "Especial"; else -> "Status" }
    val pw = when {
        mv.info.cls == 1 -> ""
        mv.info.power == -1 -> " · var."
        else -> " · ${mv.info.power}"
    }
    val shape = RoundedCornerShape(10.dp)
    Column(modifier.clip(shape).background(c.copy(alpha = 0.16f)).border(1.dp, c.copy(alpha = 0.5f), shape).padding(horizontal = 9.dp, vertical = 6.dp)) {
        Txt(mv.name, 13.5f, FontWeight.ExtraBold, maxLines = 1)
        Txt("${TypeChart.pt[mv.type]} · $cls$pw · ${mv.src}", 11f, color = pm.muted)
    }
}

// Suggested EVs and nature for the role; planned on the final form when it still evolves.
@OptIn(ExperimentalLayoutApi::class)
@Composable
fun EvBox(mon: Mon) {
    val b = Engine.buildOf(mon)
    val order = (0 until 6).filter { b.evs[it] > 0 }.sortedWith(compareByDescending<Int> { b.evs[it] }.thenBy { it })
    Column(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(10.dp)).background(pm.accentSoft).padding(horizontal = 10.dp, vertical = 8.dp),
        verticalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        Label("EVs e natureza")
        FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            for (i in order) {
                Box(Modifier.clip(RoundedCornerShape(99.dp)).background(pm.panel).padding(horizontal = 10.dp, vertical = 3.dp)) {
                    Txt("${b.evs[i]} ${Engine.statShort[i]}", 13f, FontWeight.ExtraBold, pm.accentInk)
                }
            }
        }
        Rich("Natureza: **${Engine.natureText(b)}**", 13.5f)
        if (b.target.id != mon.id) Muted("Pensando em ${b.target.name}: os EVs continuam depois de evoluir.", 12.5f)
        Muted("EVs vêm das batalhas e das vitaminas. A natureza dá para trocar com Mints.", 12f)
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
fun EvoBlock(mon: Mon, lv: Int) {
    val kids = Engine.nextEvos(mon)
    val ready = kids.any { it.evo != null && it.evo.level > 0 && lv >= it.evo.level }
    val shape = RoundedCornerShape(10.dp)
    Column(
        Modifier.fillMaxWidth().clip(shape).background(if (ready) pm.goodBg else pm.panel2).padding(horizontal = 10.dp, vertical = 8.dp),
        verticalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        if (kids.isEmpty()) Rich("**Forma final** · BST ${mon.bst}", 13f)
        for (k in kids) {
            val canEvolve = k.evo != null && k.evo.level > 0 && lv >= k.evo.level
            val finals = Engine.finalForms(k).filter { it.id != k.id }.map { it.name }
            FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                if (canEvolve) Pill("Pronto para evoluir", "good")
                Rich("→ **${k.name}**", 13f)
                Chips(k.types)
                Muted("· " + Engine.evoText(k.evo) + if (finals.isNotEmpty()) "  → " + finals.joinToString(" / ") else "")
            }
        }
    }
}

@Composable
fun Logo(width: Dp, modifier: Modifier = Modifier) {
    val wood = pm.wood
    val woodDark = pm.woodDark
    val ink = pm.ink
    Canvas(modifier.size(width, width * 44f / 64f)) {
        val s = size.width / 64f
        fun rect(x: Float, y: Float, w: Float, h: Float, color: Color, r: Float = 0f) =
            drawRoundRect(color, Offset(x * s, y * s), Size(w * s, h * s), CornerRadius(r * s))
        rect(0f, 2f, 64f, 9f, wood, 1.5f)
        for (x in listOf(0f, 24f, 48f)) rect(x, 11f, 16f, 17f, wood)
        rect(16f, 11f, 8f, 17f, ink, 2f)
        rect(40f, 11f, 8f, 17f, ink, 2f)
        rect(17.5f, 13f, 3f, 4f, Color.White, 1f)
        rect(41.5f, 13f, 3f, 4f, Color.White, 1f)
        val smile = Path().apply { moveTo(28f * s, 20.5f * s); quadraticBezierTo(32f * s, 24.5f * s, 36f * s, 20.5f * s) }
        drawPath(smile, ink, style = Stroke(width = 2f * s, cap = StrokeCap.Round))
        rect(0f, 28f, 64f, 8f, woodDark, 1.5f)
        rect(4f, 36f, 10f, 5f, woodDark, 1f)
        rect(50f, 36f, 10f, 5f, woodDark, 1f)
    }
}

@Composable
fun Wordmark(size: Float, color: Color = pm.fg) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
        Text("Palet", color = color, fontSize = size.sp, fontFamily = Bungee)
        Box(Modifier.clip(RoundedCornerShape(5.dp)).background(pm.accent).padding(horizontal = 8.dp)) {
            Text("Mon", color = pm.accentFg, fontSize = size.sp, fontFamily = Bungee)
        }
    }
}

@Composable
fun HazardStripe(modifier: Modifier = Modifier) {
    Canvas(modifier.fillMaxWidth().height(5.dp)) {
        drawRect(Color(0xFFF2B300))
        val h = size.height
        val band = 14.dp.toPx()
        var x = -h
        while (x < size.width + h) {
            val p = Path().apply {
                moveTo(x, h); lineTo(x + band, h); lineTo(x + band + h, 0f); lineTo(x + h, 0f); close()
            }
            drawPath(p, Color(0xFF1F1C19))
            x += band * 2
        }
    }
}
