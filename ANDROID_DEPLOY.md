# Android Deploy

## Was schon vorbereitet ist

- Swipe-Steuerung fuer `links`, `rechts` und `hoch` ist eingebaut.
- Android startet mit `60 FPS`, kein Screen-Sleep und Landscape-Autorotation.
- Die Android App-ID ist auf `com.fakezz.coindash` gesetzt.
- Android baut jetzt fuer `ARMv7 + ARM64`.

## Was du in Unity einmal installieren musst

In `Unity Hub` bei deiner Unity-Version:

1. `Add modules`
2. `Android Build Support`
3. `Android SDK & NDK Tools`
4. `OpenJDK`

## So bekommst du das Spiel aufs Handy

### Variante 1: Direkt per USB

1. Auf dem Android-Handy `Entwickleroptionen` aktivieren.
2. `USB-Debugging` einschalten.
3. Handy per USB mit dem PC verbinden.
4. In Unity:
   `File > Build Settings`
5. `Android` waehlen.
6. `Switch Platform` druecken, falls noch nicht aktiv.
7. Pruefen, dass diese Szenen aktiv sind:
   - `MainMenu`
   - `CasinoRun`
   - `StageSelect`
   - `LoadingScene`
8. `Build And Run` druecken.

Unity baut dann die APK und installiert sie direkt aufs Handy.

### Variante 2: APK bauen und manuell installieren

1. In Unity `File > Build Settings`.
2. `Android` auswaehlen.
3. `Build` druecken.
4. Einen Speicherort fuer die `.apk` waehlen.
5. Die APK aufs Handy kopieren.
6. Auf dem Handy Installation aus unbekannter Quelle fuer deinen Datei-Manager erlauben.
7. APK antippen und installieren.

## Wenn Unity nach einem Keystore fragt

Fuer direktes Testen auf dem eigenen Handy reicht ein neuer Debug-/Release-Keystore:

1. `Edit > Project Settings > Player > Android > Publishing Settings`
2. `Custom Keystore` aktivieren
3. `Keystore Manager` oder `Create new`
4. Passwort merken

## Wenn `Build And Run` das Handy nicht sieht

1. USB-Modus auf `Dateiuebertragung` stellen.
2. Android-Dialog fuer USB-Debugging bestaetigen.
3. Handy einmal neu anstecken.
4. In Unity Build Settings erneut pruefen.

## Empfehlung

Zum ersten Testen ist `Build And Run` die einfachste und sicherste Variante.
