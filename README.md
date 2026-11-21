# NOVA Desktop

NOVA ist jetzt eine **native Windows-Desktop-App mit WPF/.NET** – kein Browserfenster und keine HTML-Oberfläche. Das Design orientiert sich am geteilten Beispiel: dunkler Chat, schlichte Seitenleiste und eine einzelne Eingabezeile.

> **Windows x64:** [Aktuelle Version herunterladen](https://github.com/Buhawi/NOVA-Desktop/releases/latest). Das Release enthält die selbstenthaltende NOVA-App, Installer und Lizenzhinweise. GGUF-Modelle und llama.cpp-Runtimes sind nicht enthalten; lokale Inferenz erfordert eigene, separat bezogene Dateien. Alternativ können optionale Cloudanbieter mit eigenen API-Schlüsseln verwendet werden.

## Chat und Sprache

- Nachrichten können getippt oder über die Mikrofontaste diktiert werden.
- Enter sendet eine Nachricht; Shift+Enter fügt einen Zeilenumbruch ein.
- Text-to-Speech für Antworten lässt sich in den Einstellungen ein- und ausschalten und ist bei einer neuen NOVA-Konfiguration standardmäßig aus. Bestehende gespeicherte Einstellungen bleiben erhalten. Die Audio-Einstellungen bieten außerdem installierte Windows-Stimmen, Sprechtempo, Ausgabelautstärke, auswählbaren Lautsprecher, auswählbares Mikrofon samt NOVA-Eingangspegel, Erkennungssprache und Erkennungskonfidenz. Mikrofon- und Stimmprobe sind enthalten.
- Antworten werden während der Modellgenerierung satzweise vorgelesen, sobald Text ankommt. Dadurch muss die Sprachausgabe nicht mehr auf die komplette Modellantwort warten.
- Der Button **Sprache abbrechen** stoppt aktuelle und vorgemerkte Sprachausgabe. Sobald du eine neue Nachricht eintippst oder sendest, wird die alte Sprachausgabe ebenfalls sofort verworfen.
- Modellantworten erscheinen beim Streaming direkt während der Generierung im Chat, nicht erst nach Abschluss.
- Bei fehlgeschlagenen Modellantworten kannst du über **Erneut versuchen** denselben Prompt mit dem zugehörigen bisherigen Gesprächskontext erneut absenden. Die Nutzernachricht wird dabei nicht dupliziert.
- Das **＋** im Eingabefeld hängt bis zu 10 Text-/Quelldateien oder PNG-, JPEG- und WebP-Bilder an die nächste Nachricht. Für Anhänge gibt es kein festes Dateigrößenlimit mehr; sehr große Dateien können jedoch viel Arbeitsspeicher belegen oder die Anfrage-/Kontextgrenze des Modells überschreiten. Textdateien werden als Text übergeben. Bilder werden direkt an ein multimodales Modell übermittelt. Beim Cloudbetrieb fragt NOVA vor dem Übermitteln von Texten und Bildern ausdrücklich nach.
- Für direkte lokale Bilderkennung im Chat brauchst du ein multimodales GGUF-Modell, eine dazu passende `mmproj`-GGUF-Datei und eine Vision-fähige llama.cpp-Runtime. NOVA prüft bei bekannten Modellen die hinterlegte Modell-/Projektor-Zuordnung und hängt inkompatible Projektoren nicht an. Das Quality-Modell Qwen3.8-2B ist ein Textmodell; Research-Bilder werden separat vom SmolVLM2-Vision-Fallback analysiert. Hugging-Face-Modelle müssen ebenfalls Bildeingaben unterstützen. Ein normales Textmodell allein kann keine Bilder erkennen.
- Der Bereich **Research** erstellt aus bis zu 50 Quelldateien ein strukturiertes Dossier: Management-Zusammenfassung, Themenübersicht mit einem Eintrag je Kapitel, detaillierte Kapitel nach Themen, erklärende Unterthemen und Absätze sowie Quellen- und Grenzhinweise. Zusammenhänge, Begriffe, Fakten, Argumente und Unsicherheiten sollen anhand der Quellen nachvollziehbar ausgearbeitet werden. Die hierarchische Verarbeitung bleibt innerhalb des eingestellten Modell-Kontexts. Das exportierte PDF setzt Markdown-Überschriften, Absätze, Listen und Tabellen als lesbares Dokument; abgesetzte LaTeX-Formeln werden lokal als typografische Formeln gerendert, bei nicht unterstützter Syntax bleibt der Quelltext sichtbar. Überschriften erhalten mehr Abstand und werden möglichst zusammen mit dem folgenden Abschnitt auf einer Seite gehalten. Kopf-/Fußzeilen und Seitenzahlen werden ergänzt. Optional kann der PDF-Export bis zu 20 passende Beispielbilder aus Wikimedia Commons laden; NOVA berücksichtigt nur CC BY/CC BY-SA, CC0 oder gemeinfreie Bilder und druckt Urheber, Lizenz und Dateiquelle ab. Dabei wird nur der von der KI erzeugte Dokumenttitel als Suchbegriff übertragen, niemals der Inhalt der Quelldateien. Du kannst das Dossier vor dem Export bearbeiten. NOVA lässt die KI einen thematischen PDF-Dateinamen aus dem Dossier erzeugen. Falls eine separate Kapitelanfrage leer zurückkommt, übernimmt NOVA transparent die bereits erstellte KI-Zwischenzusammenfassung, statt den gesamten Research-Lauf abzubrechen. Ein eigener **Research abbrechen**-Knopf stoppt die laufende Verarbeitung. Jedes PDF darf höchstens 300 Seiten umfassen; insgesamt werden höchstens 10.000.000 extrahierte Textzeichen verarbeitet. Bei Überschreitung des Zeichenlimits wird der ausgelassene Inhalt sichtbar gekennzeichnet und die betroffenen Dateien werden genannt. Die bisherigen Byte-Limits bleiben bestehen: 2 MB pro Textdatei, 15 MB pro Bild, 40 MB Bilddaten insgesamt und 50 MB pro PDF. Lokal bleibt die Standardoption. Bei Cloudauswahl fragt NOVA vor dem Senden ausdrücklich nach; ausgelesene Texte, lokal transkribierte Audiodateien und lokale Bildbeschreibungen können dann in mehreren Schritten an den Anbieter gehen. Audio und Originalbilder werden durch Research nicht übertragen. Bilder benötigen das lokale Vision-Fallback-Modell samt Projektor; PDFs müssen auswählbaren Text enthalten (kein OCR).
- WAV-, MP3-, M4A-, AAC-, WMA- und FLAC-Dateien können im Research-Bereich lokal transkribiert werden. Beim ersten Audio-Task fragt NOVA, bevor es das rund 45 MB große deutsche `vosk-model-small-de-0.15`-Modell von alphacephei.com herunterlädt. Die Audiodateien werden nicht an einen Cloudanbieter gesendet; das Modell wird unter `%LOCALAPPDATA%\NOVA\speech-models` gespeichert und benötigt ungefähr 300 MB Arbeitsspeicher zur Laufzeit. Das Modell steht unter Apache 2.0; die kleine Variante ist ressourcenschonend, aber bei Podcasts deutlich weniger genau als große Servermodelle. Video und YouTube-Links werden nicht transkribiert.
- Während NOVA eine Antwort generiert, kannst du im Chat nach oben scrollen, um frühere Nachrichten zu lesen. NOVA folgt neuen Textteilen nur automatisch, wenn du bereits am unteren Ende des Chats bist.
- NOVA bevorzugt automatisch eine deutsche Windows-Stimme. Ohne installierte deutsche Stimme kann die TTS-Aussprache trotz deutschem Antworttext fremdsprachig klingen. Installiere in Windows ein deutsches Sprach-/Sprachsynthesepaket oder wähle eine deutsche Stimme.
- Die Audio-Einstellungen stehen direkt oben. NOVA kann Ein- und Ausgabegerät unabhängig vom Windows-Standard wählen; die Pegelregler wirken innerhalb von NOVA. Windows-Audioeinstellungen lassen sich zusätzlich direkt öffnen.
- Sprachein- und -ausgabe verwenden Windows-Sprachdienste. Bei automatischer Stimmauswahl bevorzugt NOVA eine installierte deutsche Stimme; fehlen deutsche Stimmen, sollte ein deutsches Windows-Sprachpaket installiert oder in den Einstellungen eine passende Stimme ausgewählt werden. Spracherkennung hängt ebenfalls von installierten Windows-Sprachpaketen ab.
- Unter **Antwortformat** lässt sich freier Text oder ein JSON-Objekt auswählen. Weitere Generierungsoptionen sind Temperatur, Antwortlänge und Stop-Sequenzen.
- NOVA kann die lokale Uhrzeit nennen und auf ausdrücklichen Wunsch nach Bestätigung Websites oder Websuchen öffnen.
- Wenn du NOVA ausdrücklich bittest, ein Programm zu erstellen, kann das Modell über Werkzeuge Dateien im lokalen Ordner `Dokumente\NOVA-Workspace` auflisten, mehrere Quelldateien auf einmal erstellen/überschreiben und ein `.NET`-Projekt mit `dotnet build` kompilieren. In den Einstellungen kannst du unter **Automatisierung und Berechtigungen** optional das Erstellen/Überschreiben von Dateien in diesem Ordner ohne Einzelbestätigung erlauben. Standardmäßig ist diese Option aus. Wenn sie aus ist, zeigt NOVA vor Änderungen ein kompaktes Fenster mit **Ja** und **Überspringen**. Builds fragen unabhängig von dieser Einstellung immer einzeln nach. Bei Compilerfehlern kann das Modell Quelldateien korrigieren und erneute Builds anfragen. Dafür wird das .NET SDK benötigt. Builds können NuGet-Pakete laden und MSBuild-Buildschritte ausführen. Prüfe generierte Dateien und bestätige Builds nur, wenn du dem Inhalt vertraust. NOVA führt keine beliebigen Shell-Befehle aus; andere Compiler oder Buildsysteme werden nicht automatisch ausgeführt.
- Der **Coder**-Tab ist ein lokaler Coding-Chat mit dauerhaft nebeneinander sichtbarem Projektkontext links und Chat rechts. Wähle einen Projektordner aus; NOVA kann bis zu 30 Text-/Quelldateien pro Vorschlag erstellen oder ändern. Unterstützt das ausgewählte Modell keine Werkzeugaufrufe oder liefert es bei einer Codeaufgabe nur Text statt eines Dateivorschlags, übernimmt das lokale Chatmodell automatisch den Datei-Fallback anhand der Hauptmodell-Antwort und des Projektkontexts. Die vollständigen Inhalte bleiben im Tab **Änderungen** prüfbar; erst **Übernehmen** schreibt sie. Vor dem Schreiben prüft NOVA, ob Dateien seit dem Vorschlag geändert wurden, und weist Pfad-Traversal sowie Verknüpfungen aus dem Projektordner heraus zurück. Der Coder erstellt Text-/Quelldateien und Projektdateien, aber keine Binärdateien; er führt keine Shell-Befehle aus, installiert keine Pakete und startet keine Builds oder Tests.
- Der Hamburger-Button bleibt in der Chat-Kopfleiste erreichbar, auch wenn die Seitenleiste ausgeblendet ist.

## Gesprächsverlauf und Kontextfreigabe

Unterhaltungen werden lokal als JSON-Dateien in `%LOCALAPPDATA%\NOVA\conversations` gespeichert und erscheinen unter **Verlauf**. Ein Klick lädt eine Unterhaltung samt Nachrichten. Über das Papierkorb-Symbol am jeweiligen Eintrag kannst du einzelne Chats löschen; NOVA fragt vorher nach Bestätigung. Das Löschen ist dauerhaft. Über **＋** an einem Verlaufseintrag kann diese Unterhaltung als zusätzlicher Kontext für den aktuellen Chat freigegeben werden; NOVA fragt vor dem Übermitteln nochmals ausdrücklich nach. Ein erneuter Klick auf den Kontext-Eintrag widerruft die Freigabe für diesen Chat. Wenn NOVA auf eine frühere Unterhaltung Bezug benötigt, kann sie über ein Werkzeug ebenfalls eine Auswahl und eine separate Bestätigung anfordern. Ohne diese Bestätigung wird kein alter Gesprächsinhalt in eine Modellanfrage aufgenommen. Pro Freigabe werden höchstens 20 Nachrichten und 10.000 Zeichen verwendet. Ein neuer Chat übernimmt den Kontext nicht.

## Lokale Modelle verwenden

1. Lege deine GGUF-Dateien im Ordner `models` **neben `NOVA.exe`** ab. Im Quellcode liegt der Entwicklungsordner im Projektstamm unter `models`.
2. Wähle eine passende Runtime aus dem Paket oder stelle eine eigene Windows-Version von `llama-server.exe` bereit.
3. Starte NOVA, klicke oben rechts auf den Modellnamen oder unten links auf ⚙ und wähle **Lokal mit llama.cpp (GGUF)**.
4. Wähle dein Modell aus der Liste. Falls du Dateien hinzufügst, während NOVA geöffnet ist, aktualisiere die Liste mit ↻.

Nur GGUF-Dateien direkt im `models`-Ordner werden angezeigt; Unterordner werden nicht durchsucht. Beim Entwickeln erkennt NOVA außerdem die gebündelten Modelle unter `dist\models`, wenn der Projektordner `models` leer ist. Die Modelldateien werden nicht hochgeladen. Die Auswahl und der llama-server-Pfad werden unter `%LOCALAPPDATA%\NOVA\settings.json` gespeichert. Große Modelle benötigen entsprechend viel Arbeitsspeicher; das erste Laden kann eine Weile dauern.

### Gebündelte lokale Modell-Presets

Die Pocket-Presets verwenden dieselben GGUF-Modelle wie die Android-App. Die übrigen Profile sind zusätzliche Desktop-Modelle:

| Preset | Exakte Datei | Hinweis |
|---|---|---|
| Pocket · Performance | `Qwen3-0.6B-Q4_K_M.gguf` | Schnelles 0,6B-Textmodell; Bildaufgaben nutzen den separaten SmolVLM2-500M-Fallback |
| Pocket · Ultra-Performance | `SmolVLM2-256M-Video-Instruct-Q4_K_M.gguf` | Sehr kleines Text-/Bildmodell mit `mmproj-SmolVLM2-256M-Video-Instruct-Q8_0.gguf`; einfachere Antworten |
| Performance | `DeepSeek-R1-Distill-Qwen-1.5B-Q8_0.gguf` | Kompaktes 1,5B-Modell |
| Quality | `Qwen3.8-2B-Q8_0.gguf` | Allgemeines 2B-Modell |
| Ultra-Quality | `WikiQwen-4B.Q4_K_M.gguf` | Spezialisiertes WikiQwen; nicht als allgemeines Modell gedacht |
| Coder · Quality | `Qwen2.5-Coder-7B-Instruct-Q4_K_M.gguf` | Spezialisiertes 7B-Codermodell; benötigt deutlich mehr Arbeitsspeicher |

Die globalen Presets findest du unter **Einstellungen → Anbieter „Lokal“ → Lokale Modell-Presets**. Dort lassen sich unter **Weitere Modellordner** zusätzliche Ordner auf internen oder externen Laufwerken sowie Netzwerkfreigaben (UNC-Pfade) hinzufügen. GGUF-Dateien direkt in diesen Ordnern werden gemeinsam mit `models` für Chat, Research und Coder angeboten; Unterordner werden nicht durchsucht. Netzwerkordner müssen beim Laden erreichbar und lesbar sein. Nach dem Laden liegt das Modell im Arbeitsspeicher; der erste Ladevorgang kann über ein Netzwerk langsamer sein. Zusätzlich haben **Research** und **Coder** jeweils eine eigene Modellauswahl; diese wird dauerhaft gespeichert und der Modellserver bei einem Modellwechsel neu gestartet. Die Pocket-Profile und die drei allgemeinen Profile stehen in den Arbeitsbereichen zur Verfügung, sofern die jeweilige GGUF-Datei vorhanden ist. Die Pocket-Performance-Bilderkennung verwendet das separate SmolVLM2-500M-Modell samt Projektor; Ultra-Performance verwendet ihr eigenes 256M-Modell und den passenden Projektor. Das Quality-Modell Qwen3.8-2B wird als Textmodell verwendet; Research-Bilder werden an das separate SmolVLM2-Vision-Fallback geleitet. Der gleichnamige Flash-Next-Projektor ist nicht mit der bereitgestellten 2B-Datei kompatibel und wird dafür nicht geladen. Das 7B-Codermodell ist zusätzlich für Codeentwürfe auswählbar. Nicht spezialisierte Modelle können Codeaufgaben ebenfalls versuchen, sind aber kein Ersatz für ein trainiertes Codermodell. GGUF-Modelle werden nicht automatisch geladen oder heruntergeladen; das deutsche Vosk-Sprachmodell wird nur nach Bestätigung beim ersten Audio-Task nachgeladen. Chat, Research und Coder zeigen während der Arbeit eine schmale Fortschrittsanzeige; eine gemeinsame Schaltfläche **Aufgabe abbrechen** stoppt laufende Chat-, Research- und Coder-Anfragen, im Research-Bereich zusätzlich über **Research abbrechen**.

Wähle in den Einstellungen die gewünschte Runtime; NOVA zeigt Backend, Version und den exakten Pfad an. Das lokal erstellte vollständige `dist`-Paket enthält CPU-, CUDA-13.4- und Vulkan-Runtimes samt llama.cpp-Dateien. Das GitHub-Release enthält diese großen Runtime- und Modelldateien nicht. Die CUDA-Runtime kann zusätzlich passende CUDA-Bibliotheken erfordern; die Vulkan-Runtime benötigt `vulkan-1.dll` aus dem Grafiktreiber. Prüfe, dass Systemtreiber und Runtime zueinander passen.

Die Standardeinstellung fordert 99 GPU-Layer an; das ist weder ein Nachweis, dass eine GPU gewählt wurde, noch dass Layer tatsächlich ausgelagert wurden. Vor einem lokalen Start mit einer im Runtime-Katalog ausgewählten CUDA-Runtime und angeforderten GPU-Layern prüft NOVA mit `--list-devices`, ob CUDA eine NVIDIA-GPU erkennt. Falls nicht, prüft NOVA ausschließlich die gebündelte Vulkan-Runtime aus demselben Paketordner. Wenn Vulkan ebenfalls keine NVIDIA-GPU bestätigt (oder die Vulkan-Runtime fehlt), startet NOVA mit einer verfügbaren CPU-Runtime und setzt deren GPU-Layer auf 0. Auch wenn ein GPU-Modellserver beim Start abstürzt oder das Zeitlimit überschreitet, versucht NOVA den CPU-Fallback. NOVA zeigt die aktive Runtime und den Fallback-Grund an; die gespeicherte CUDA-/Vulkan-Auswahl und die angeforderten GPU-Layer bleiben unverändert. Ohne angeforderte GPU-Layer wird die CUDA-Geräteprüfung nicht durchgeführt. Für zusätzliche llama.cpp-Windows-ZIPs kannst du sie in `llama-runtimes\CPU`, `CUDA` oder `Vulkan` neben `NOVA.exe` legen und **ZIPs einlesen**. Verwende nur vertrauenswürdige Archive.

GPU-Erkennung und tatsächlicher Modell-Offload hängen von Hardware, Treibern und Runtime-Version ab. Prüfe die von NOVA angezeigte aktive Runtime; die bloße Geräteerkennung garantiert nicht, dass ein Modell erfolgreich geladen oder auf die GPU ausgelagert wurde.

## NOVA installieren

Im erstellten `dist`-Ordner startest du `Install-NOVA.cmd`. Der Installer kopiert NOVA nach `%LOCALAPPDATA%\Programs\NOVA` und erstellt eine Desktop-Verknüpfung mit dem NOVA-Appsymbol. Modelle und llama.cpp-Runtimes aus dem Paketordner werden mitkopiert. Die Kopie nutzt `robocopy /E` (kein Spiegeln/Löschen): zusätzliche Dateien und Modelle, die nur im Installationsordner liegen, werden nicht entfernt; gleichnamige Dateien aus dem Paket können aber aktualisiert bzw. überschrieben werden. Einstellungen und Gesprächsverläufe liegen unter `%LOCALAPPDATA%\NOVA`, außerhalb des Installationsordners, und werden von diesem Skript nicht kopiert oder gelöscht. Sichere eigene Modelle mit gleichem Dateinamen vor einem Update vorsichtshalber separat. Für eine Verteilung kannst du den gesamten `dist`-Ordner als ZIP weitergeben; der Empfänger entpackt ihn und startet `Install-NOVA.cmd`.

### Kompaktes Kernpaket für die Weitergabe

Nach einem erfolgreichen NOVA-Build kannst du mit `.\Build-NOVA-CoreBundle.ps1` ein separates `core-bundle` erzeugen, ohne vorhandene Dateien in `dist` zu verändern. Das Paket enthält NOVA, das Qwen3.8-2B-Textmodell, die beiden Pocket-Profile einschließlich ihrer Bildmodelle und passenden Projektoren sowie eine CPU-Runtime und bleibt unter 4,1 GiB. Damit sind Chat, Research, Bildanalyse und einfache Codeentwürfe ohne separate Modell- oder GPU-Downloads verfügbar; ein spezialisiertes 7B-Codermodell ist nicht enthalten. Das Skript bricht ab, wenn Voraussetzungen fehlen oder das Paketlimit überschritten wird. Die CPU-Runtime ist langsamer als eine passende GPU-Runtime.

Für Empfänger: `core-bundle` entpacken und `Install-NOVA.cmd` starten. `README-Core-Bundle.txt`, `MODEL-SOURCES.txt` und `LICENSES` liegen direkt im Paket.

Wenn keine eigene Runtime konfiguriert ist, erkennt NOVA die gebündelte Runtime automatisch und bevorzugt CUDA, danach Vulkan und CPU. Das reine Kernpaket enthält nur CPU und ist damit direkt startfähig.

CUDA ist üblicherweise die beste Wahl für NVIDIA-Grafikkarten, wenn die CUDA-Runtime und Treiber zur ZIP passen. Vulkan ist breiter kompatibel (insbesondere bei AMD/Intel unter Windows), kann aber je nach GPU langsamer sein. CPU funktioniert ohne GPU-Unterstützung, ist für große Modelle aber in der Regel deutlich langsamer. Welche Variante optimal ist, hängt von GPU, Treibern und llama.cpp-Build ab; probiere die passenden Versionen mit demselben Modell aus. `--mlock` ist optional: Wenn eine Runtime die Option nicht unterstützt, deaktiviert NOVA sie automatisch und versucht den Start erneut.

Das Farbschema lässt sich unter **Einstellungen → Darstellung** auf **Dunkel** oder **Hell** stellen und wird für NOVA, Einstellungen, Coder und interne Dialoge übernommen; die Windows-Titelleiste wird ebenfalls angepasst. Systemdialoge wie Datei-Auswahl richten sich weiterhin nach dem Windows-Farbschema. Systemprompt, Kontext, GPU-Layer, CPU-Threads, Batch-Größen, parallele Anfragen, Flash Attention, Speichersperre, Temperatur, Antwortlänge, Denkbudget und Stop-Sequenzen lassen sich direkt in den NOVA-Einstellungen festlegen. Kürzere Antworten (geringere maximale Antwortlänge) und ein kleinerer Denkaufwand können die Ausgabe beschleunigen. Für Startwerte der Hardware-Optionen können in `.env` neben `NOVA.exe` beispielsweise folgende Werte gesetzt werden:

Eigene Systemprompts werden als benutzerdefinierte Regeln eingefügt und ausdrücklich zur präzisen Befolgung angewiesen. NOVA wiederholt zusätzlich Deutsch als Antwortsprache und die Format-/Sicherheitsprioritäten. Ein Prompt kann das Verhalten eines kleinen oder nicht gut instruierten Modells jedoch nicht vollständig garantieren.

```env
LOCAL_CONTEXT_SIZE=4096
LOCAL_GPU_LAYERS=99
LOCAL_CPU_THREADS=6
LOCAL_BATCH_SIZE=512
LOCAL_UBATCH_SIZE=128
LOCAL_PARALLEL=1
LOCAL_FLASH_ATTENTION=false
LOCAL_MLOCK=false
```

## Cloudanbieter (optional)

Unter **Einstellungen → Modell-Anbieter** stehen lokal mit llama.cpp sowie Google Gemini, DeepSeek, OpenRouter und Hugging Face zur Auswahl. Die Modell-ID lässt sich pro Anbieter einstellen. NOVA speichert API-Schlüssel im Windows-Anmeldeinformationsspeicher, nicht in `settings.json`. Alternativ können `GEMINI_API_KEY`, `DEEPSEEK_API_KEY`, `OPENROUTER_API_KEY` oder `HF_TOKEN` in eine `.env`-Datei neben `NOVA.exe` eingetragen werden; `.env.example` enthält die Variablennamen.

- Gemini verwendet standardmäßig `gemini-3.8-flash`; DeepSeek `deepseek-flash`. Bei OpenRouter kann die Modell-ID angegeben oder das Konto-Standardmodell leer gelassen werden. Hugging-Face-Modellnamen können ebenfalls angepasst werden.
- Bei ausgewähltem Cloudanbieter werden Chatnachrichten an diesen Anbieter gesendet; NOVA fragt zusätzlich vor Datei- und Bildanhängen. Cloud-Research fragt einmal vor dem gesamten mehrstufigen Lauf und weist auf mögliche Gebühren hin. Die Auswahl eines Cloudanbieters allein überträgt noch keine Research-Dateien.
- OpenRouter-Fallbacks auf andere Anbieter sind in NOVA standardmäßig deaktiviert. Preise, Gratisquoten, Modellverfügbarkeit und Datenverarbeitung unterscheiden sich je Anbieter und können sich ändern; eine dauerhaft kostenlose Cloudnutzung ist nicht garantiert. Prüfe vor dem Einsatz die jeweiligen Bedingungen: [Gemini API](https://ai.google.dev/gemini-api/docs), [DeepSeek API](https://api-docs.deepseek.com/), [OpenRouter](https://openrouter.ai/docs), [Hugging Face Inference Providers](https://huggingface.co/docs/inference-providers).

## Windows-EXE erstellen

Voraussetzung zum Erstellen ist das **.NET 8 SDK für Windows**. Das fertige Programm lässt sich ohne separate .NET-Runtime starten:

```powershell
.\build-exe.ps1
```

Falls Windows die Skriptausführung in deiner PowerShell-Sitzung blockiert, kannst du das Skript nur für diesen Aufruf starten:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-exe.ps1
```

Danach liegt die selbstenthaltende 64-Bit-Anwendung unter `dist\NOVA.exe`. Das Skript aktualisiert ausschließlich die Quellprojekt-Ausgabe `dist` und bricht ab, wenn genau diese `dist\NOVA.exe` gerade läuft; es aktualisiert nicht automatisch `%LOCALAPPDATA%\Programs\NOVA`. Eine installierte Kopie wird separat über `Install-NOVA.cmd` aktualisiert. Lege neben die EXE GGUF-Dateien und bei Bedarf eine `.env`-Datei. Beachte beim Installationsskript den Hinweis zum Ersetzen gleichnamiger Modell-Dateien oben.

Der Build erzeugt eine native WPF-Anwendung für Windows x64. Er muss auf Windows erfolgen; eine macOS- oder Linux-Ausgabe ist damit nicht enthalten.

## Lizenz

Für den NOVA-eigenen Quellcode wird keine Open-Source-Lizenz gewährt; alle Rechte vorbehalten. Die öffentliche Bereitstellung auf GitHub ist keine Erlaubnis zur Nutzung, Änderung oder Weiterverteilung. Lizenzen und Hinweise der mitgelieferten Drittanbieterkomponenten stehen unter [`LICENSES/`](./LICENSES/) und in [`THIRD-PARTY-NOTICES.md`](./THIRD-PARTY-NOTICES.md).
