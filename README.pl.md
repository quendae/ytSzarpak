<p align="center">
  <img src="branding/icon-source.png" width="96" alt="Ikona YTSzarpak">
</p>

<h1 align="center">YTSzarpak</h1>

<p align="center">
  Mała, wieloplatformowa aplikacja desktopowa do pobierania multimediów przez <a href="https://github.com/yt-dlp/yt-dlp">yt-dlp</a> — bez ciągłego korzystania z terminala.
</p>

<p align="center">
  <a href="README.md">English</a> · <strong>Polski</strong> · <a href="README.de.md">Deutsch</a>
</p>

---

YTSzarpak powstał na te sytuacje, kiedy yt-dlp jest dokładnie tym narzędziem, którego potrzebujesz, ale nie masz ochoty po raz kolejny wpisywać tych samych komend.

Wklejasz link, wybierasz format, dodajesz plik do kolejki i pozwalasz aplikacji zająć się szczegółami wiersza poleceń. Działa z YouTube oraz z wieloma innymi serwisami obsługiwanymi przez yt-dlp.

## Co potrafi

- **Pobieranie wideo i audio** przy użyciu yt-dlp.
- **Wybór jakości** na podstawie formatów rzeczywiście dostępnych dla danego linku.
- **Tryb tylko audio** z MP3 i innymi popularnymi formatami wyjściowymi.
- **Kolejka pobierania** z postępem, prędkością, ETA i akcjami dla każdego elementu.
- **Obsługa playlist** po wklejeniu adresu playlisty.
- **Logowanie do YouTube** przez ciasteczka z przeglądarki lub wyeksportowany plik `cookies.txt`.
- **Automatyczne pobieranie yt-dlp i sprawdzanie aktualizacji.**
- **Automatyczne przygotowanie FFmpeg**, jeśli aplikacja nie znajdzie działającej instalacji systemowej.
- Jedna baza kodu **Avalonia/.NET** dla Windows, macOS i Linux.

## Jak to działa

1. Wklej obsługiwany adres do materiału.
2. Kliknij **Grab**, aby YTSzarpak sprawdził dostępne formaty.
3. Wybierz jakość wideo albo przełącz się na tryb tylko audio.
4. Dodaj materiał do kolejki.
5. Możesz dodawać kolejne linki, podczas gdy pobieranie trwa w tle.

Aplikacja nie próbuje pisać własnego downloadera od zera. Jest desktopowym interfejsem dla yt-dlp i FFmpeg, a oba narzędzia pozostają osobnymi, wymienialnymi komponentami.

## Logowanie do YouTube

Część materiałów YouTube jest dostępna tylko dla zalogowanych użytkowników.

YTSzarpak nigdy nie prosi o hasło do konta Google. Może za to użyć ciasteczek z profilu przeglądarki, w którym jesteś już zalogowany. Jeśli odczyt z przeglądarki nie działa — szczególnie Chrome w Windows potrafi blokować bazę ciasteczek — wyeksportuj plik `cookies.txt` w formacie Netscape i wskaż go w **Ustawieniach**. Jeżeli skonfigurowane są obie metody, plik cookies ma pierwszeństwo.

## FFmpeg

FFmpeg jest potrzebny między innymi do łączenia osobnych strumieni obrazu i dźwięku oraz do konwersji audio.

Jeśli YTSzarpak nie znajdzie FFmpeg w systemie, pobierze kompatybilny build do własnego katalogu danych aplikacji. W **Ustawieniach** możesz też ręcznie wskazać konkretną instalację FFmpeg.

## Budowanie ze źródeł

Potrzebujesz **.NET 10 SDK**.

```bash
git clone https://github.com/quendae/ytSzarpak.git
cd ytSzarpak
dotnet build
```

Uruchomienie aplikacji desktopowej:

```bash
dotnet run --project src/YtDlpGui.App
```

## Wersje do dystrybucji

Repozytorium zawiera skrypty publikujące dla poszczególnych systemów. Tworzą one samowystarczalne buildy, więc użytkownik końcowy nie musi osobno instalować .NET ani Pythona.

| System | Polecenie | Wynik |
| --- | --- | --- |
| Windows | `publish\publish-windows.ps1` | `publish\output\win-x64\YTSzarpak.exe` |
| macOS | `./publish/publish-macos.sh` | `publish/output/osx-{x64,arm64}/YTSzarpak.app` |
| Linux | `./publish/publish-linux.sh` | `publish/output/linux-x64/YTSzarpak` |

## Kilka szczegółów technicznych

YTSzarpak jest zbudowany na **Avalonia 12** i **.NET 10**. Stan interfejsu obsługuje `CommunityToolkit.Mvvm`, a pobieranie oraz zarządzanie binariami znajduje się w osobnym projekcie `YtDlpGui.Core`.

Przy pierwszym użyciu yt-dlp jest pobierany jako samodzielny plik wykonywalny i zapisywany w katalogu danych aplikacji. YTSzarpak może sprawdzać nowsze wersje yt-dlp i aktualizować zarządzaną kopię bez potrzeby instalowania Pythona czy pip.

## Projekty zewnętrzne

YTSzarpak korzysta z kilku świetnych projektów open source:

- [yt-dlp](https://github.com/yt-dlp/yt-dlp) — wyszukiwanie źródeł i pobieranie multimediów.
- [FFmpeg](https://ffmpeg.org/) — przetwarzanie, łączenie i konwersja multimediów.
- [Avalonia UI](https://avaloniaui.net/) — wieloplatformowy interfejs desktopowy.

Każdy z tych projektów zachowuje własną licencję i warunki dystrybucji. Samo repozytorium YTSzarpak nie zawiera obecnie osobnego pliku licencji.

---

<p align="center">
  Prosty desktopowy interfejs dla bardzo rozbudowanego narzędzia konsolowego.
</p>
