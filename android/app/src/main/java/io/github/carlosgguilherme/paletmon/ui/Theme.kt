package io.github.carlosgguilherme.paletmon.ui

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.ReadOnlyComposable
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import io.github.carlosgguilherme.paletmon.R

// Palet Mon palette: Amarelo Paleteira, Madeira, Grafite, Concreto.
@Immutable
data class PmColors(
    val bg: Color, val panel: Color, val panel2: Color, val line: Color, val fg: Color, val muted: Color,
    val accent: Color, val accentFg: Color, val accentInk: Color, val accentSoft: Color,
    val wood: Color, val woodDark: Color, val ink: Color,
    val good: Color, val warn: Color, val bad: Color, val goodBg: Color, val warnBg: Color, val badBg: Color, val immBg: Color,
)

private val Light = PmColors(
    bg = Color(0xFFE9E7E2), panel = Color(0xFFF8F7F4), panel2 = Color(0xFFDEDBD4), line = Color(0xFFCBC6BC), fg = Color(0xFF1F1C19), muted = Color(0xFF615A50),
    accent = Color(0xFFF2B300), accentFg = Color(0xFF1F1C19), accentInk = Color(0xFF8A6400), accentSoft = Color(0xFFFBEEC4),
    wood = Color(0xFFC4874A), woodDark = Color(0xFF8A5A2E), ink = Color(0xFF1F1C19),
    good = Color(0xFF1F8A5B), warn = Color(0xFFB7791F), bad = Color(0xFFC2362F), goodBg = Color(0xFFD6EFE0), warnBg = Color(0xFFF6E7C8), badBg = Color(0xFFF6D6D3), immBg = Color(0xFFCFD8E8),
)

private val Dark = PmColors(
    bg = Color(0xFF171512), panel = Color(0xFF211E1A), panel2 = Color(0xFF2B2722), line = Color(0xFF3A352E), fg = Color(0xFFEFEAE2), muted = Color(0xFFA39A8D),
    accent = Color(0xFFF5BC1A), accentFg = Color(0xFF1F1C19), accentInk = Color(0xFFF5BC1A), accentSoft = Color(0xFF3A3018),
    wood = Color(0xFFD0965A), woodDark = Color(0xFF9A6A3C), ink = Color(0xFF1F1C19),
    good = Color(0xFF4CC78C), warn = Color(0xFFE0A84A), bad = Color(0xFFEF6B62), goodBg = Color(0xFF183A29), warnBg = Color(0xFF3D3018), badBg = Color(0xFF432020), immBg = Color(0xFF26304A),
)

val LocalPm = staticCompositionLocalOf { Light }
val pm: PmColors
    @Composable @ReadOnlyComposable get() = LocalPm.current

val Bungee = FontFamily(Font(R.font.bungee))

@Composable
fun PaletMonTheme(content: @Composable () -> Unit) {
    val dark = isSystemInDarkTheme()
    val c = if (dark) Dark else Light
    val scheme = if (dark) {
        darkColorScheme(primary = c.accent, onPrimary = c.accentFg, background = c.bg, surface = c.panel, onSurface = c.fg, onBackground = c.fg)
    } else {
        lightColorScheme(primary = c.accentInk, onPrimary = Color.White, background = c.bg, surface = c.panel, onSurface = c.fg, onBackground = c.fg)
    }
    CompositionLocalProvider(LocalPm provides c) {
        MaterialTheme(colorScheme = scheme, content = content)
    }
}
