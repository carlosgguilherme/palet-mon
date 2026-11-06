#!/usr/bin/env bash
# Installs the APK on the running emulator, uses every screen through its visible text and saves screenshots.
apk="$1"
out="$2"
pkg=io.github.carlosgguilherme.paletmon
mkdir -p "$out"

shot() { sleep 1; adb exec-out screencap -p > "$out/$1.png"; }
alive() { adb shell pidof "$pkg" > /dev/null; }
fail() {
  echo "FALHOU: $1"
  shot falha
  adb logcat -d > "$out/logcat.txt"
  exit 1
}

# Center of the element with this exact text, or else the first one containing it.
find_text() {
  adb exec-out uiautomator dump /dev/tty 2>/dev/null | python3 -c '
import html, re, sys
want = sys.argv[1]
exact, near = None, None
for node in re.findall(r"<node [^>]*>", sys.stdin.read()):
    text = re.search(r" text=\"([^\"]*)\"", node)
    box = re.search(r"bounds=\"\[(\d+),(\d+)\]\[(\d+),(\d+)\]\"", node)
    if not text or not box:
        continue
    value = html.unescape(text.group(1))
    x1, y1, x2, y2 = map(int, box.groups())
    center = f"{(x1 + x2) // 2} {(y1 + y2) // 2}"
    if value == want and exact is None:
        exact = center
    elif want in value and near is None:
        near = center
print(exact or near or "")
' "$1"
}

wait_text() {
  local limit="${2:-20}"
  for _ in $(seq 1 "$limit"); do
    [ -n "$(find_text "$1")" ] && return 0
    sleep 1
  done
  return 1
}

tap_text() {
  wait_text "$1" "${2:-20}" || fail "não achei \"$1\""
  adb shell input tap $(find_text "$1")
  sleep 1
}

swipe_up() { adb shell input swipe $((w / 2)) $((h * 70 / 100)) $((w / 2)) $((h * 30 / 100)) 250; sleep 1; }
scroll_to() {
  for _ in $(seq 1 14); do
    [ -n "$(find_text "$1")" ] && return 0
    swipe_up
  done
  fail "não achei \"$1\" rolando a tela"
}

# the emulator's own "not responding" popups would swallow the taps
adb shell settings put global hide_error_dialogs 1
adb install -r "$apk" || fail "instalação"
adb shell appops set "$pkg" REQUEST_INSTALL_PACKAGES allow
sleep 5
adb logcat -c
adb shell am start -W -n "$pkg/$pkg.MainActivity" | tee "$out/inicio.txt"

size="$(adb shell wm size | tail -1 | sed -E 's/.*: *//' | tr -d '\r')"
w="${size%x*}"
h="${size#*x}"
echo "tela: $w x $h"

# the test build is older than the latest release, so opening has to offer the update
if wait_text "Agora não" 25; then
  shot 00-oferta-de-atualizacao
  tap_text "Agora não"
else
  echo "AVISO: a oferta de atualização não apareceu"
  shot 00-sem-oferta
fi

echo "--- Meu time"
wait_text "cobertura" 30 || fail "tela inicial não abriu"
shot 01-meu-time
tap_text "Sprigatito"
wait_text "NÍVEL" || fail "cartão não abriu"
shot 02-cartao-aberto
swipe_up
shot 02b-cartao-rolado
tap_text "Defesa"
wait_text "Dano recebido" || fail "tabela de defesa"
shot 03-defesa
tap_text "Ataque"
wait_text "Melhor dano" || fail "tabela de ataque"
shot 04-ataque
scroll_to "Escolher para Montar time"
shot 05-sugestoes

echo "--- Montar"
tap_text "Montar"
tap_text "Adicionar Pokémon"
wait_text "Nome do Pokémon" || fail "busca não abriu"
shot 06-busca
tap_text "Nome do Pokémon"
adb shell input text Garchomp
sleep 2
shot 06b-busca-digitada
tap_text "Garchomp"
wait_text "Garchomp" || fail "Pokémon não foi escolhido"
scroll_to "Montar time em volta deles"
shot 07-montar
tap_text "Montar time em volta deles"
wait_text "melhor encaixe" 40 || fail "montar time não terminou"
shot 08-montado
scroll_to "Usar como meu time"
tap_text "Usar como meu time"
wait_text "cobertura" || fail "usar time não voltou para Meu time"
wait_text "Garchomp" || fail "time montado não entrou"
shot 09-time-montado

echo "--- Gerador"
tap_text "Gerador"
tap_text "Gerar time"
wait_text "Time gerado" 40 || fail "gerador não terminou"
shot 10-gerado
swipe_up
shot 10b-gerado-rolado

echo "--- Batalha"
tap_text "Batalha"
wait_text "Contra quem" || fail "batalha não abriu"
shot 11-batalha
tap_text "Por Pokémon"
tap_text "Escolher Pokémon adversário"
wait_text "Nome do Pokémon" || fail "busca do adversário não abriu"
tap_text "Nome do Pokémon"
adb shell input text Gyarados
sleep 2
tap_text "Gyarados"
wait_text "Trocar adversário" || fail "adversário não foi escolhido"
shot 12-adversario
scroll_to "Como jogar"
shot 12b-como-jogar

echo "--- Modo de combate"
tap_text "Singles 6x6"
wait_text "Modo de combate" || fail "painel de modo não abriu"
shot 13-modo
tap_text "Regra pronta"
tap_text "Duplas 4 de 6 · Nv 50 (estilo VGC)"
wait_text "Modo de combate" || fail "painel de modo não voltou"
shot 14-modo-duplas
adb shell input keyevent KEYCODE_BACK
sleep 2
alive || fail "app fechou ao usar o Voltar no painel"
wait_text "Duplas 4 de 6" || fail "modo não mudou no topo"

echo "--- Sobre e tema escuro"
tap_text "Sobre"
wait_text "O que é o Palet Mon" || fail "sobre não abriu"
shot 15-sobre
adb shell cmd uimode night yes
sleep 3
shot 16-sobre-escuro
tap_text "Meu time"
shot 17-time-escuro
adb shell cmd uimode night no
sleep 3

echo "--- Rolagem"
adb shell dumpsys gfxinfo "$pkg" reset > /dev/null
for _ in 1 2 3 4 5 6; do swipe_up; done
adb shell dumpsys gfxinfo "$pkg" > "$out/gfxinfo.txt"

echo "--- Fechar e reabrir"
tap_text "Sobre"
adb shell input keyevent KEYCODE_BACK
wait_text "cobertura" || fail "Voltar não foi para Meu time"
adb shell input keyevent KEYCODE_BACK
sleep 3
adb shell am force-stop "$pkg"
adb shell am start -W -n "$pkg/$pkg.MainActivity" | tee -a "$out/inicio.txt"
wait_text "Agora não" 15 && tap_text "Agora não"
wait_text "Duplas 4 de 6" 30 || fail "o modo salvo se perdeu ao reabrir"
wait_text "Garchomp" || fail "o time salvo se perdeu ao reabrir"
shot 18-reaberto

echo "--- Atualização"
tap_text "Sobre"
scroll_to "Verificar atualizações"
shot 19-atualizacoes
tap_text "Verificar atualizações"
if wait_text "Baixando a versão" 20; then
  shot 20-baixando
  wait_text "Confirme a instalação" 180 || fail "o download da atualização não terminou"
  sleep 3
  shot 21-instalador
  adb shell dumpsys window | grep -E 'mCurrentFocus' | tee "$out/instalador.txt"
  grep -qi 'packageinstaller' "$out/instalador.txt" || fail "o instalador do Android não abriu"
else
  echo "AVISO: sem atualização para baixar"
  shot 20-sem-atualizacao
fi

adb logcat -d > "$out/logcat.txt"
if grep -q "FATAL EXCEPTION" "$out/logcat.txt"; then
  grep -A16 "FATAL EXCEPTION" "$out/logcat.txt" | head -40
  echo "houve erro fatal"
  exit 1
fi
echo "tudo certo"
