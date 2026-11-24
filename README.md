# Momentum Groves Mobile

The two sections below describe the same app and setup. English comes first, followed by Danish.

De to afsnit nedenfor beskriver den samme app og opsætning. Engelsk kommer først, derefter dansk.

[English](#english) · [Dansk](#dansk)

---

## English

- [Overview](#overview)
- [Requirements and installation](#requirements-and-installation)
- [PC setup and sync](#pc-setup-and-sync)
- [Using the app](#using-the-app)
- [Data and privacy](#data-and-privacy)
- [Project structure](#project-structure)
- [Testing and publishing](#testing-and-publishing)
- [Troubleshooting](#troubleshooting)

### Overview

Momentum Groves Mobile is the Android companion to the Momentum Groves web app. Use it to save foods away from your PC and take your training programmes to the gym. You can read downloaded programmes and change exercise weights while the PC is off. When you’re back at the PC, one tap syncs both sections.

| Home screen | What it does |
| --- | --- |
| **Training** | View downloaded programmes, exercise details, and cached pictures. Edit weights in kilograms and save them on the phone. |
| **Food** | Add, edit, and delete pending foods. Scan a barcode to fill in nutrition from Open Food Facts. |
| **Sync to PC** | Send pending foods and weight changes, fetch programme updates, and show a result for each section. |

### Requirements and installation

- .NET 10 SDK with the `maui-android` workload.
- Android SDK Platform 36 for compiling, plus a compatible JDK. Use the Android and Java SDK paths configured in your IDE or local build settings.
- An Android device or emulator running Android 7.0 (API 24) or later. Barcode scanning needs a usable camera.
- The separate Momentum Groves web project for sync and the full training editor. You can start entering foods before pairing.

Clone or download this repository, open its root directory, and run:

```powershell
dotnet --version
dotnet workload restore MomentumGroves.Mobile.csproj
dotnet build MomentumGroves.Mobile.csproj -c Debug
```

If no Android workload is installed, run `dotnet workload install maui-android` with the .NET 10 SDK. If the build reports `XA5207`, install Android SDK Platform 36 in your Android SDK manager.

To run from Rider, select an Android device or emulator and run the mobile project. For manual installation, the Debug build produces `bin/Debug/net10.0-android/dk.momentumgroves.privatefood-Signed.apk`. Install the APK on your own device. Android may ask you to allow installation from the app opening the file.

### PC setup and sync

1. Start the updated Momentum Groves web app. Set `MobileSync__Key` in its local environment or ignored `.env` file. Use a private key of at least 16 characters.
2. Configure the web app to serve HTTPS at an address the phone can reach on your private network. Use a certificate Android trusts, with a name that matches the address you enter. For example, `https://pc.example.com:5443` illustrates the format. Use your actual PC address.
3. In the mobile app, open **Food → Saved foods**. Enter the PC’s HTTPS address and the same key, then tap **Save sync settings**.
4. Return to the mobile home screen and tap **Sync to PC**. The first successful training sync downloads your existing programmes.

Sync runs once per tap and stops when finished. Opening a screen, saving an item, and changing networks do not start sync. If the PC is unreachable, pending changes stay on the phone for your next tap.

The phone’s `localhost` points to the phone itself. If the PC web app listens only on its own loopback address, the phone cannot connect. Both devices can still use their locally saved data. See the web project’s README for desktop setup and database initialization.

### Using the app

**Training.** Open a programme group, enter a weight in kilograms, and tap **Save weight**. Saving works offline after the first programme download. If both devices change the same exercise weight before sync, review the PC value in **Training**. Save the phone value again to use it, or discard unresolved phone edits to keep the PC values. Programme creation, exercise management, and workout history are available in **Open full editor (online)**. If the PC training page is already open after a sync, click **Reload programmes** to see the latest weights.

**Food.** Open **Add food**, set the unit to grams or millilitres, and enter nutrition per 100 g or 100 ml. Calories, protein, carbs, and fat are required. Fiber is optional. You can scan an EAN or UPC barcode to prefill the form when Open Food Facts has the product. Review the values and save. The PC adds the item to its food library and skips an existing item with the same name. The phone removes entries only after the PC acknowledges their batch.

### Data and privacy

| Data | Where it lives |
| --- | --- |
| Pending foods | `foods.json` in Android app data. |
| Downloaded programmes and pending weights | A local JSON file in Android app data, scoped to the configured PC address. |
| Downloaded exercise pictures | Android app cache. Android can clear cached pictures. |
| PC address and sync key | The address is stored in app preferences. The key is stored in MAUI secure storage. |

Clearing app storage or uninstalling can remove unsynced changes. The PC stores synced data in its own SQLite databases. Phone-to-PC sync sends the key in the `X-Momentum-Sync-Key` header over HTTPS. Barcode lookup sends the scanned product code to Open Food Facts over HTTPS. Manual food entry and downloaded training programmes work without that service.

### Project structure

| Path | Purpose |
| --- | --- |
| [`MomentumGroves.Mobile.csproj`](MomentumGroves.Mobile.csproj) | Android target, MAUI and ZXing dependencies, and asset registration. |
| [`Views/`](Views/) | Home menu, food entry, barcode scanner, and online and offline training screens. |
| [`src/Service/`](src/Service/) | Local storage, barcode lookup, and manual food and training sync. |
| [`DTO/`](DTO/) | Food data and transfer models. |
| [`Platforms/Android/`](Platforms/Android/) | Android entry points, permissions, and launcher icon settings. |
| [`Resources/`](Resources/) | Logo, launcher assets, and splash screen. |

### Testing and publishing

1. Save a food and change a downloaded exercise weight with the PC offline. Reopen the app and confirm both changes are still saved.
2. Start the PC web app, tap **Sync to PC**, and check that the food appears in the food library and the exercise weight updates in Training.
3. Change a programme on the PC, tap **Sync to PC** on Android, and confirm the update appears on the phone.
4. Try a sync while the PC is off. Confirm that the phone reports the failure and keeps pending changes for the next tap.

Keep real sync keys, `.env` files, signing keys, private databases, exports, and local SDK paths out of Git. The [.gitignore](.gitignore) covers common local files and build output. Use your own release signing credentials when distributing an APK or app bundle. Include only images you have permission to redistribute.

### Troubleshooting

| Symptom | What to check |
| --- | --- |
| The build reports `NETSDK1139` or a missing workload. | Check that your terminal or IDE is using .NET 10 and has the MAUI Android workload. |
| The build reports a missing `android.jar`. | Install Android SDK Platform 36 in the SDK used by the build. |
| The phone cannot connect. | Check that the PC web app is running, the address is reachable from the phone, and the certificate matches that address and is trusted by Android. Check the PC’s private-network firewall rules if needed. |
| The PC rejects the key or reports a missing endpoint. | Check that both apps use the same key and that the PC is running the updated web app. |
| Training is empty on the phone. | Confirm the PC has saved programmes and complete the first **Sync to PC**. |
| The scanner cannot find a food. | Check camera permission and internet access. Enter the food manually if the barcode or nutrition data is missing. |

---

## Dansk

- [Overblik](#overblik)
- [Krav og installation](#krav-og-installation)
- [Pc-opsætning og synkronisering](#pc-opsætning-og-synkronisering)
- [Brug af appen](#brug-af-appen)
- [Data og privatliv](#data-og-privatliv)
- [Projektstruktur](#projektstruktur)
- [Test og publicering](#test-og-publicering)
- [Fejlfinding](#fejlfinding)

### Overblik

Momentum Groves Mobile er Android-appen til Momentum Groves-webappen. Brug den til at gemme madvarer væk fra din pc og tage dine træningsprogrammer med i træningscenteret. Du kan se downloadede programmer og ændre træningsvægte, mens pc’en er slukket. Når du er tilbage ved pc’en, kan du synkronisere begge områder med ét tryk.

| Startmenu | Funktion |
| --- | --- |
| **Training** | Se downloadede programmer, øvelsesoplysninger og gemte billeder. Redigér vægte i kilogram, og gem dem på telefonen. |
| **Food** | Tilføj, redigér og slet madvarer, der afventer synkronisering. Scan en stregkode for at udfylde næringsværdier fra Open Food Facts. |
| **Sync to PC** | Send ventende madvarer og vægtændringer, hent programopdateringer, og se resultatet for hvert område. |

### Krav og installation

- .NET 10 SDK med `maui-android`-workloaden.
- Android SDK Platform 36 til kompilering samt en kompatibel JDK. Brug stierne til Android SDK og Java SDK fra dit udviklingsmiljø eller dine lokale buildindstillinger.
- En Android-enhed eller emulator med Android 7.0 (API 24) eller nyere. Stregkodescanning kræver et fungerende kamera.
- Det separate Momentum Groves-webprojekt til synkronisering og den fulde programeditor. Du kan begynde at registrere madvarer, før du forbinder de to apps.

Klon eller download dette repository, åbn rodmappen, og kør:

```powershell
dotnet --version
dotnet workload restore MomentumGroves.Mobile.csproj
dotnet build MomentumGroves.Mobile.csproj -c Debug
```

Hvis Android-workloaden mangler, skal du køre `dotnet workload install maui-android` med .NET 10 SDK. Hvis buildet melder `XA5207`, skal du installere Android SDK Platform 36 i din Android SDK Manager.

For at starte appen fra Rider skal du vælge en Android-enhed eller emulator og køre mobilprojektet. Til manuel installation opretter Debug-buildet `bin/Debug/net10.0-android/dk.momentumgroves.privatefood-Signed.apk`. Installér APK-filen på din egen enhed. Android kan bede om tilladelse til at installere fra den app, der åbner filen.

### Pc-opsætning og synkronisering

1. Start den opdaterede Momentum Groves-webapp. Angiv `MobileSync__Key` i dens lokale miljø eller i den ignorerede `.env`-fil. Brug en privat nøgle på mindst 16 tegn.
2. Konfigurér webappen til at lytte på en HTTPS-adresse, som telefonen kan nå på dit private netværk. Brug et certifikat, som Android har tillid til, og hvis navn matcher den adresse, du indtaster. Eksemplet `https://pc.example.com:5443` viser formatet. Brug din pc’s faktiske adresse.
3. Åbn **Food → Saved foods** i mobilappen. Indtast pc’ens HTTPS-adresse og den samme nøgle, og tryk på **Save sync settings**.
4. Gå tilbage til mobilappens startside, og tryk på **Sync to PC**. Den første vellykkede træningssynkronisering henter dine eksisterende programmer.

Synkroniseringen kører én gang pr. tryk og stopper, når den er færdig. Åbning af en skærm, lagring af en post og skift af netværk starter ingen synkronisering. Hvis telefonen ikke kan nå pc’en, bliver ventende ændringer på telefonen indtil næste tryk.

Telefonens `localhost` peger på selve telefonen. Hvis webappen kun lytter på pc’ens egen loopback-adresse, kan telefonen ikke forbinde. Begge enheder kan stadig bruge deres lokalt gemte data. Se webprojektets README for pc-opsætning og initialisering af databaser.

### Brug af appen

**Træning.** Åbn en programgruppe, indtast en vægt i kilogram, og tryk på **Save weight**. Du kan gemme offline efter den første download af programmerne. Hvis begge enheder ændrer vægten for den samme øvelse før synkronisering, kan du se pc’ens vægt under **Training**. Gem telefonens vægt igen for at bruge den, eller kassér de uløste ændringer på telefonen for at beholde pc’ens værdier. Oprettelse af programmer, redigering af øvelser og træningshistorik findes under **Open full editor (online)**. Hvis træningssiden allerede er åben på pc’en efter synkronisering, skal du klikke på **Reload programmes** for at se de seneste vægte.

**Madvarer.** Åbn **Add food**, vælg gram eller milliliter, og indtast næringsindhold pr. 100 g eller 100 ml. Kalorier, protein, kulhydrat og fedt er obligatoriske. Kostfibre er valgfrie. Du kan scanne en EAN- eller UPC-stregkode for at udfylde formularen, når Open Food Facts har produktet. Kontrollér værdierne, og gem. Pc’en tilføjer varen til sit madbibliotek og springer en eksisterende vare med samme navn over. Telefonen fjerner først poster, når pc’en har kvitteret for den pågældende gruppe af poster.

### Data og privatliv

| Data | Placering |
| --- | --- |
| Ventende madvarer | `foods.json` i Android-appens data. |
| Downloadede programmer og ventende vægte | En lokal JSON-fil i Android-appens data, knyttet til den konfigurerede pc-adresse. |
| Downloadede øvelsesbilleder | Android-appens cache. Android kan rydde billederne fra cachen. |
| Pc-adresse og synkroniseringsnøgle | Adressen ligger i appens indstillinger. Nøglen ligger i MAUI Secure Storage. |

Hvis du rydder appens lager eller afinstallerer den, kan du miste ændringer, der afventer synkronisering. Pc’en gemmer synkroniserede data i sine egne SQLite-databaser. Ved synkronisering sender telefonen nøglen i headeren `X-Momentum-Sync-Key` over HTTPS. Stregkodeopslag sender den scannede produktkode til Open Food Facts over HTTPS. Manuel madregistrering og downloadede træningsprogrammer virker uden denne tjeneste.

### Projektstruktur

| Sti | Formål |
| --- | --- |
| [`MomentumGroves.Mobile.csproj`](MomentumGroves.Mobile.csproj) | Android-målplatform, MAUI- og ZXing-afhængigheder samt registrering af grafik. |
| [`Views/`](Views/) | Startmenu, madregistrering, stregkodescanner og træningssider til online- og offlinebrug. |
| [`src/Service/`](src/Service/) | Lokal lagring, stregkodeopslag og manuel synkronisering af mad og træning. |
| [`DTO/`](DTO/) | Datamodeller til madvarer og overførsel. |
| [`Platforms/Android/`](Platforms/Android/) | Android-startpunkter, tilladelser og indstillinger for appikonet. |
| [`Resources/`](Resources/) | Logo, appikonets grafik og startskærm. |

### Test og publicering

1. Gem en madvare, og ændr vægten for en downloadet øvelse, mens pc’en er offline. Åbn appen igen, og kontrollér, at begge ændringer stadig er gemt.
2. Start webappen på pc’en, tryk på **Sync to PC**, og kontrollér, at varen vises i madbiblioteket, og at vægten opdateres under Training.
3. Ændr et program på pc’en, tryk på **Sync to PC** på Android, og kontrollér, at opdateringen vises på telefonen.
4. Forsøg at synkronisere, mens pc’en er slukket. Kontrollér, at telefonen viser fejlen og beholder ventende ændringer til næste tryk.

Hold ægte synkroniseringsnøgler, `.env`-filer, signeringsnøgler, private databaser, eksportfiler og lokale SDK-stier ude af Git. [.gitignore](.gitignore) dækker almindelige lokale filer og buildoutput. Brug dine egne release-signeringsnøgler, hvis du distribuerer en APK eller app bundle. Medtag kun billeder, du har tilladelse til at distribuere.

### Fejlfinding

| Symptom | Hvad du skal kontrollere |
| --- | --- |
| Buildet melder `NETSDK1139` eller en manglende workload. | Kontrollér, at terminalen eller dit udviklingsmiljø bruger .NET 10 med MAUI Android-workloaden. |
| Buildet melder, at `android.jar` mangler. | Installér Android SDK Platform 36 i det SDK, som buildet bruger. |
| Telefonen kan ikke forbinde. | Kontrollér, at pc’ens webapp kører, at telefonen kan nå adressen, og at certifikatet matcher adressen og er godkendt af Android. Kontrollér om nødvendigt pc’ens firewallregler for private netværk. |
| Pc’en afviser nøglen eller melder, at et endpoint mangler. | Kontrollér, at begge apps bruger samme nøgle, og at den opdaterede webapp kører på pc’en. |
| Training er tom på telefonen. | Kontrollér, at pc’en har gemte programmer, og gennemfør den første **Sync to PC**. |
| Scanneren kan ikke finde en madvare. | Kontrollér kameratilladelse og internetadgang. Indtast varen manuelt, hvis stregkoden eller næringsværdierne mangler. |
