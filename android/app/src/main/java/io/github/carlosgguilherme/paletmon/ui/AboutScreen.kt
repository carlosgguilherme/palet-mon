package io.github.carlosgguilherme.paletmon.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import io.github.carlosgguilherme.paletmon.AppModel
import io.github.carlosgguilherme.paletmon.BuildConfig
import io.github.carlosgguilherme.paletmon.logic.Updater

@Composable
fun AboutScreen(model: AppModel) {
    val links = LocalUriHandler.current
    val version = BuildConfig.VERSION_NAME
    Page {
        item {
            Column(
                Modifier.fillMaxWidth().clip(RoundedCornerShape(18.dp)).background(pm.ink).padding(18.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Logo(96.dp)
                Wordmark(25f, Color.White)
                Txt("Seu time, bem empilhado.", 15f, FontWeight.Bold, Color.White)
                Txt("Versão $version", 13f, color = Color.White.copy(alpha = 0.75f))
            }
        }
        item {
            Card {
                SectionTitle("O que é o Palet Mon")
                Txt("Um estrategista de time para o Cobbleverse, o modpack de Pokémon para Minecraft. Ele mostra o que seu time aguenta, o que falta e quem levar para cada luta.")
                Rich("**Meu time:** fraquezas, cobertura de ataque, golpes sugeridos e quando cada Pokémon evolui.")
                Rich("**Montar:** você escolhe os Pokémon que quer usar e o app completa o resto em volta deles.")
                Rich("**Gerador:** cria um time do zero, como Aleatório, Sinergia ou Competitivo.")
                Rich("**Batalha:** diz quem mandar contra um tipo ou um Pokémon adversário.")
                Rich("**Modo de combate:** toque no botão do topo para ajustar tudo para Singles, Duplas, Triplas e regras como Nv 50 ou Little Cup.")
            }
        }
        item {
            Card {
                SectionTitle("Atualizações")
                Txt("Sempre que abre, o app procura uma versão nova e pergunta se você quer atualizar. Para isso, ele precisa de internet. Seu time e suas opções continuam salvos.")
                BigButton(model.updateLabel, enabled = !model.updateBusy) { model.checkNow() }
                Muted("Você está na versão $version.", 13f, Modifier.fillMaxWidth(), TextAlign.Center)
            }
        }
        item {
            Card {
                SectionTitle("Créditos")
                Txt("Dados e imagens dos Pokémon vêm da PokéAPI. As imagens são baixadas pela internet e ficam guardadas no aparelho.")
                Txt("Palet Mon é um projeto de fã, sem ligação com Nintendo, The Pokémon Company ou Cobblemon.")
                Muted("Golpes marcados \"TM/tutor/ovo\" precisam de TM, tutor ou reprodução. O Cobblemon segue os dados de perto, mas alguns métodos de evolução mudam no mod.")
                BigButton("Abrir no GitHub", primary = false) { links.openUri(Updater.REPO_URL) }
            }
        }
    }
}
