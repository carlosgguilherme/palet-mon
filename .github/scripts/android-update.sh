#!/usr/bin/env bash
# Installs an older APK and lets it update itself from the latest GitHub release.
apk="$1"
out="$2"
expected="$3"
pkg=io.github.carlosgguilherme.paletmon
mkdir -p "$out"

shot() { adb exec-out screencap -p > "$out/$1.png"; }
version() { adb shell dumpsys package "$pkg" | grep -m1 versionName | sed -E 's/.*versionName=//' | tr -d '\r'; }

adb shell settings put global hide_error_dialogs 1
adb install -r "$apk"
echo "instalada: $(version)"
adb shell appops set "$pkg" REQUEST_INSTALL_PACKAGES allow
sleep 20
adb logcat -c
adb shell am start -W -n "$pkg/$pkg.MainActivity"
sleep 35
shot 1-pergunta

size="$(adb shell wm size | tail -1 | sed -E 's/.*: *//' | tr -d '\r')"
w="${size%x*}"
h="${size#*x}"
adb shell input tap $((w / 2)) $((h * 858 / 1000)) # "Atualizar agora"
sleep 6
shot 2-baixando

found=""
for _ in $(seq 1 60); do
  if adb shell dumpsys window | grep -E "mCurrentFocus|mFocusedApp" | grep -q -i "packageinstaller"; then found=1; break; fi
  sleep 5
done
sleep 4
shot 3-instalador
if [ -z "$found" ]; then
  adb logcat -d > "$out/logcat.txt"
  echo "o instalador do Android nao abriu"
  exit 1
fi

adb shell uiautomator dump /sdcard/ui.xml > /dev/null
adb pull /sdcard/ui.xml "$out/ui.xml" > /dev/null
bounds="$(grep -oE 'text="(Update|Install)"[^>]*bounds="\[[0-9]+,[0-9]+\]\[[0-9]+,[0-9]+\]"' "$out/ui.xml" | head -1 | sed -E 's/.*bounds="//')"
echo "botao do instalador: $bounds"
read -r x1 y1 x2 y2 <<< "$(echo "$bounds" | sed -E 's/\[([0-9]+),([0-9]+)\]\[([0-9]+),([0-9]+)\]"?/\1 \2 \3 \4/')"
adb shell input tap $(((x1 + x2) / 2)) $(((y1 + y2) / 2))
sleep 30
shot 4-depois

adb logcat -d > "$out/logcat.txt"
now="$(version)"
echo "versao depois: $now"
if [ "$now" != "$expected" ]; then
  echo "nao atualizou para $expected"
  exit 1
fi
adb shell am start -W -n "$pkg/$pkg.MainActivity"
sleep 20
shot 5-reaberto
echo "tudo certo"
