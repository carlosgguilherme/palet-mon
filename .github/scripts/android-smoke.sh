#!/usr/bin/env bash
# Installs the APK on the running emulator, uses the main screens and saves screenshots.
apk="$1"
out="$2"
pkg=io.github.carlosgguilherme.paletmon
mkdir -p "$out"

shot() { adb exec-out screencap -p > "$out/$1.png"; }
alive() { adb shell pidof "$pkg" > /dev/null; }

# the emulator's own "not responding" popups would swallow the taps
adb shell settings put global hide_error_dialogs 1
adb install -r "$apk"
sleep 20
adb logcat -c
adb shell am start -W -n "$pkg/$pkg.MainActivity" | tee "$out/inicio.txt"
sleep 30
shot 01-abertura

size="$(adb shell wm size | tail -1 | sed -E 's/.*: *//' | tr -d '\r')"
w="${size%x*}"
h="${size#*x}"
echo "tela: $w x $h"
tab() { adb shell input tap $((w * (2 * $1 + 1) / 10)) $((h * 93 / 100)); sleep 7; }

tab 1; shot 02-montar
tab 2; shot 03-gerador
adb shell input tap $((w / 2)) $((h * 13 / 100))
sleep 20
shot 04-time-gerado
tab 3; shot 05-batalha
tab 4; shot 06-sobre
tab 0; shot 07-meu-time

# battle mode sheet: opens from the top chip and closes with Back without leaving the app
adb shell input tap $((w * 85 / 100)) $((h * 8 / 100))
sleep 5
shot 08-modo
adb shell input keyevent KEYCODE_BACK
sleep 4
shot 09-modo-fechado
alive || { echo "app fechou ao usar o Voltar no painel"; adb logcat -d > "$out/logcat.txt"; exit 1; }

# leaving with Back and opening again must not crash
adb shell input keyevent KEYCODE_BACK
sleep 6
adb shell am start -W -n "$pkg/$pkg.MainActivity"
sleep 20
shot 10-reaberto

adb logcat -d > "$out/logcat.txt"
if grep -q "AndroidRuntime: Process: $pkg" "$out/logcat.txt"; then
  grep -A12 "AndroidRuntime: Process: $pkg" "$out/logcat.txt" | head -30
  echo "houve erro fatal"
  exit 1
fi
alive || { echo "app nao reabriu"; exit 1; }
echo "tudo certo"
