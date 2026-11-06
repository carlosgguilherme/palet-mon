#!/usr/bin/env bash
# The path of someone who already has the app: installs the old published APK, updates from
# inside it through GitHub and checks that the new version opens with the saved state.
old_apk="$1"
expected="$2"
out="$3"
pkg=io.github.carlosgguilherme.paletmon
mkdir -p "$out"

shot() { sleep 1; adb exec-out screencap -p > "$out/$1.png"; }
fail() {
  echo "FALHOU: $1"
  shot falha
  adb logcat -d > "$out/logcat.txt"
  exit 1
}

# center of the element with this text; the old app has no accessibility tree, so this is for the new one and the installer
find_text() {
  adb exec-out uiautomator dump /dev/tty 2>/dev/null | python3 -c '
import html, re, sys
for node in re.findall(r"<node [^>]*>", sys.stdin.read()):
    text = re.search(r" text=\"([^\"]*)\"", node)
    box = re.search(r"bounds=\"\[(\d+),(\d+)\]\[(\d+),(\d+)\]\"", node)
    if text and box and sys.argv[1].lower() == html.unescape(text.group(1)).lower():
        x1, y1, x2, y2 = map(int, box.groups())
        print((x1 + x2) // 2, (y1 + y2) // 2)
        break
' "$1"
}

wait_text() {
  for _ in $(seq 1 "${2:-30}"); do
    [ -n "$(find_text "$1")" ] && return 0
    sleep 1
  done
  return 1
}

version() { adb shell dumpsys package "$pkg" | grep -m1 versionName | sed 's/.*=//' | tr -d '\r'; }

adb shell settings put global hide_error_dialogs 1
adb install -r "$old_apk" || fail "instalar a versão antiga"
adb shell appops set "$pkg" REQUEST_INSTALL_PACKAGES allow
sleep 15
echo "instalado: $(version)"

size="$(adb shell wm size | tail -1 | sed -E 's/.*: *//' | tr -d '\r')"
w="${size%x*}"
h="${size#*x}"

adb shell am start -W -n "$pkg/$pkg.MainActivity"
sleep 30
shot 01-app-antigo-oferta

# closes the offer, opens the Sobre tab (saved in the state) and opens the app again
adb shell input tap $((w / 2)) $((h * 15 / 100))
sleep 4
adb shell input tap $((w * 9 / 10)) $((h * 93 / 100))
sleep 6
shot 02-app-antigo-sobre
adb shell am force-stop "$pkg"
sleep 2
adb shell am start -W -n "$pkg/$pkg.MainActivity"
sleep 30
shot 03-app-antigo-reaberto

# top to bottom: stops at the first button, because the sheet then has only the progress bar
for y in 76 79 82 85; do
  adb shell input tap $((w / 2)) $((h * y / 100))
  sleep 1
done
sleep 2
shot 04-baixando

for _ in $(seq 1 120); do
  adb shell dumpsys window | grep -E 'mCurrentFocus' > "$out/instalador.txt"
  grep -qi 'packageinstaller' "$out/instalador.txt" && break
  sleep 2
done
grep -qi 'packageinstaller' "$out/instalador.txt" || fail "o instalador do Android não abriu"
sleep 3
shot 05-instalador

wait_text "Update" 20 || fail "botão do instalador"
adb shell input tap $(find_text "Update")

for _ in $(seq 1 60); do
  [ "$(version)" = "$expected" ] && break
  sleep 2
done
now="$(version)"
echo "depois de atualizar: $now"
[ "$now" = "$expected" ] || fail "a versão continuou $now"
shot 06-atualizado

adb shell am force-stop "$pkg"
adb shell am start -W -n "$pkg/$pkg.MainActivity"
wait_text "O que é o Palet Mon" 30 || fail "a aba salva no app antigo se perdeu"
shot 07-app-novo-com-estado-salvo

adb logcat -d > "$out/logcat.txt"
if grep -q "FATAL EXCEPTION" "$out/logcat.txt" && grep -A3 "FATAL EXCEPTION" "$out/logcat.txt" | grep -q "$pkg"; then
  echo "houve erro fatal"
  exit 1
fi
echo "tudo certo"
