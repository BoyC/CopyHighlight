# Selection Copy Flash

A small Visual Studio VSIX that flashes the **selection area** (not the text foreground) when selected text is copied.

<img width="819" height="387" alt="CopyHighlight" src="https://github.com/user-attachments/assets/f54f5d69-c1fe-41e9-b448-f080ccdf25d1" />

This is a small editor feature that [Gargaj](https://github.com/Gargaj) originally built into the game client of [Perpetuum](http://www.perpetuum-online.com/) because "everyone has pasted stuff into IRC they didn't want to". It's been a staple of all UI systems I wrote for the past 20 years and now with the power of Codex I brought it to Visual Studio finally (since who has time to figure out the whole VS plugin system and adornment API for something as small as this)

## Config

Settings for flash brightness and duration can be found in the VS Options dialog under Selection Copy Flash:

<img width="744" height="434" alt="image" src="https://github.com/user-attachments/assets/12b108c2-ee41-4abe-adc9-3c809e00e2e9" />

For light themes the brightness can be set to negative values to darken instead of brighten the color.

## Build and run

1. Open `CopyHighlight.sln` in Visual Studio 2022.
2. Let NuGet restore the SDK packages.
3. Build the solution.
4. Install `bin\Debug\SelectionCopyFlash.vsix` or `bin\Release\SelectionCopyFlash.vsix` manually.
5. In Visual Studio:
   - open a code file or the Output window
   - select text
   - press `Ctrl+C`
6. Optional: open `Tools > Options > Selection Copy Flash > General` and tune the animation.
