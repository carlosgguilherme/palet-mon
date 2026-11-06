package io.github.carlosgguilherme.paletmon

import android.app.Application
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import io.github.carlosgguilherme.paletmon.data.Dex
import io.github.carlosgguilherme.paletmon.logic.AppState
import io.github.carlosgguilherme.paletmon.logic.BattleMode
import io.github.carlosgguilherme.paletmon.logic.Engine
import io.github.carlosgguilherme.paletmon.logic.GenResult
import io.github.carlosgguilherme.paletmon.logic.Member
import io.github.carlosgguilherme.paletmon.logic.TeamSlot
import io.github.carlosgguilherme.paletmon.logic.UpdateOffer
import io.github.carlosgguilherme.paletmon.logic.Updater
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class UpdateAction(val label: String, val primary: Boolean = true, val run: () -> Unit)
class UpdateNotice(val text: String, val actions: List<UpdateAction>, val progress: Boolean = false)

class AppModel(app: Application) : AndroidViewModel(app) {
    var ready by mutableStateOf(false)
        private set
    var state by mutableStateOf(AppState())
        private set

    var buildResults by mutableStateOf<List<GenResult>?>(null)
    var building by mutableStateOf(false)
        private set
    var genResult by mutableStateOf<GenResult?>(null)
    var genEmpty by mutableStateOf(false)
    var generating by mutableStateOf(false)
        private set

    var notice by mutableStateOf<UpdateNotice?>(null)
    var updateBusy by mutableStateOf(false)
        private set
    var updateLabel by mutableStateOf(IDLE_LABEL)
        private set
    var progress by mutableIntStateOf(0)
        private set

    private val updater = Updater(app)
    private val filesDir = app.filesDir
    private val buildShown = HashSet<String>()
    private val genShown = HashSet<String>()
    private var saveJob: Job? = null

    init {
        viewModelScope.launch(Dispatchers.Default) {
            Dex.load(app.assets.open("dex.json").bufferedReader().use { it.readText() })
            val loaded = AppState.load(filesDir)
            Engine.s = loaded
            withContext(Dispatchers.Main) {
                state = loaded
                ready = true
                autoCheck()
            }
            // fills the evolution and move caches before the first search or team generation
            for (p in Dex.all) { Engine.nextEvos(p); Engine.featOf(p) }
        }
    }

    fun update(change: (AppState) -> AppState) {
        val next = change(state)
        state = next
        Engine.s = next
        saveJob?.cancel()
        saveJob = viewModelScope.launch(Dispatchers.IO) {
            delay(300)
            AppState.save(next, filesDir)
        }
    }

    fun setTab(tab: String) = update { it.copy(tab = tab) }

    fun setMode(mode: BattleMode) {
        update { it.copy(mode = mode.copy(pick = minOf(mode.pick, mode.size))) }
        buildResults = null; genResult = null; genEmpty = false
        buildShown.clear(); genShown.clear()
    }

    fun wishChanged(change: (List<Int>) -> List<Int>) {
        update { it.copy(wish = change(it.wish)) }
        buildChanged()
    }

    fun buildChanged() { buildResults = null; buildShown.clear() }
    fun genChanged() { genShown.clear() }

    fun build(fresh: Boolean) {
        if (building) return
        if (fresh) buildShown.clear()
        building = true
        viewModelScope.launch {
            val r = withContext(Dispatchers.Default) { Engine.buildAround(buildShown) }
            buildResults = r
            building = false
        }
    }

    fun generate() {
        if (generating) return
        generating = true
        viewModelScope.launch {
            val r = withContext(Dispatchers.Default) { Engine.generateBoxes(state.gen, genShown) }
            genEmpty = r == null || r.team.isEmpty()
            genResult = if (genEmpty) null else r
            generating = false
        }
    }

    fun useAsTeam(team: List<Member>) {
        update { st ->
            val fallback = st.team.maxOfOrNull { it.lv } ?: 50
            st.copy(
                tab = "time",
                team = team.map { z ->
                    val owned = st.team.firstOrNull { Dex.byId[it.id]?.chain == z.p.chain }
                    owned ?: TeamSlot(z.p.id, fallback, z.ab)
                },
            )
        }
    }

    // ---------- updates ----------
    private fun close(label: String = "Fechar") = UpdateAction(label, primary = false) { notice = null }

    private fun setBusy(busy: Boolean, label: String = IDLE_LABEL) {
        updateBusy = busy
        updateLabel = label
    }

    private fun sizeText(o: UpdateOffer) = if (o.bytes > 0) " (${maxOf(1, o.bytes / (1024 * 1024))} MB)" else ""

    // Quiet check when the app opens: only says something if there is a new version.
    private fun autoCheck() {
        viewModelScope.launch {
            try {
                val offer = updater.check()
                if (offer != null && !updateBusy) {
                    notice = UpdateNotice(
                        "Nova versão disponível: **${offer.version}**. Para atualizar você precisa estar com internet${sizeText(offer)}.",
                        listOf(UpdateAction("Atualizar agora") { install(offer) }, close("Agora não")),
                    )
                }
            } catch (_: Exception) {
            }
        }
    }

    fun checkNow() {
        if (updateBusy) return
        setBusy(true, "Procurando…")
        viewModelScope.launch {
            try {
                val offer = updater.check()
                if (offer == null) {
                    setBusy(false)
                    notice = UpdateNotice("Você já está na versão mais recente (${BuildConfig.VERSION_NAME}).", listOf(close()))
                } else install(offer)
            } catch (_: Exception) {
                setBusy(false)
                notice = UpdateNotice("Não consegui procurar atualizações. Confira sua internet e tente de novo.", listOf(close()))
            }
        }
    }

    private fun install(offer: UpdateOffer) {
        setBusy(true, "Atualizando…")
        progress = 0
        notice = UpdateNotice("Baixando a versão **${offer.version}**. Depois é só confirmar a instalação na tela do Android.", emptyList(), progress = true)
        viewModelScope.launch {
            try {
                updater.download(offer) { p -> progress = p }
                AppState.save(state, filesDir)
                openInstaller()
            } catch (_: Exception) {
                setBusy(false)
                notice = UpdateNotice("A atualização não terminou. Nada mudou no app: confira sua internet e tente de novo.", listOf(close()))
            }
        }
    }

    private fun openInstaller() {
        val pending = updater.install()
        setBusy(false)
        notice = UpdateNotice(pending, listOf(UpdateAction("Instalar") { openInstaller() }, close()))
    }

    companion object {
        private const val IDLE_LABEL = "Verificar atualizações"
    }
}
