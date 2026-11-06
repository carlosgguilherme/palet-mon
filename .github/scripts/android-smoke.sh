#!/usr/bin/env bash
# Installs the APK on the running emulator, opens every tab and saves screenshots.
apk="$1"
out="$2"
pkg=io.github.carlosgguilherme.paletmon
mkdir -p "$out"

shot() { adb exec-out screencap -p > "$out/$1.png"; }

adb install -r "$apk"
adb logcat -c
adb shell am start -W -n "$pkg/$pkg.MainActivity" | tee "$out/inicio.txt"
sleep 30
shot 1-abertura

size="$(adb shell wm size | tail -1 | sed -E 's/.*: *//' | tr -d '\r')"
w="${size%x*}"
h="${size#*x}"
echo "tela: $w x $h"
y=$((h * 93 / 100))
for i in 0 1 2 3 4; do
  adb shell input tap $((w * (2 * i + 1) / 10)) "$y"
  sleep 8
  shot "aba-$i"
done

if adb shell pidof "$pkg" > /dev/null; then
  echo "app continua aberto"
else
  echo "app fechou sozinho"
  adb logcat -d > "$out/logcat.txt"
  exit 1
fi

# leaving with Back and opening again must not crash
adb shell input keyevent KEYCODE_BACK
sleep 6
shot depois-de-voltar
adb shell am start -W -n "$pkg/$pkg.MainActivity"
sleep 20
shot reaberto

adb logcat -d > "$out/logcat.txt"
if grep -q "AndroidRuntime: Process: $pkg" "$out/logcat.txt"; then
  grep -A12 "AndroidRuntime: Process: $pkg" "$out/logcat.txt" | head -30
  echo "houve erro fatal"
  exit 1
fi
adb shell pidof "$pkg" > /dev/null || { echo "app nao reabriu"; exit 1; }
echo "tudo certo"
