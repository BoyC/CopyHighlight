# Selection Copy Flash

A small Visual Studio VSIX that flashes the **selection area** (not the text foreground) when selected text is copied.

<img width="819" height="387" alt="CopyHighlight" src="https://github.com/user-attachments/assets/f54f5d69-c1fe-41e9-b448-f080ccdf25d1" />

## Behavior

- Trigger: `Edit.Copy` / `Ctrl+C`
- Scope:
  - normal editor text views
  - common Output window text views (`Output`, `BuildOutput`, `BuildOrderOutput`, `DebugOutput`, `TestsOutput`)
- Animation:
  - default duration: ~0.20 seconds
  - interpolation: **perceptually linear** color blending in Oklab, which looks visually more even than raw sRGB interpolation
  - visible color path: brighter selection appearance -> original selection appearance
  - implementation detail: the flash samples Visual Studio's own selection adornments and uses each real selection visual as an opacity mask, so it follows the shape currently painted by the editor instead of reconstructing selection geometry
- Settings:
  - `Tools > Options > Selection Copy Flash > General`
  - `Duration (ms)`
  - `Brightness (%)`

The extension intentionally does **nothing** when the selection is empty.

## How it works

1. A chained editor command handler intercepts `CopyCommandArgs`.
2. It calls Visual Studio's built-in copy handler.
3. It paints a viewport-relative flash rectangle.
4. Each flash rectangle uses a `VisualBrush` of the corresponding editor-created selection adornment as its opacity mask.
5. A frame-synchronized render loop computes the target visible selection color, solves the overlay needed to produce it over the existing selection, and then fades that overlay all the way to transparent.
6. The options page stores duration and brightness in Visual Studio's user settings store.

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

## Notes

- The project builds against .NET Framework 4.8.1 reference assemblies but the VSIX does not declare a separate .NET Framework installer dependency.
- If you want to support additional specialized panes, add more `[ContentType(...)]` attributes to `CopySelectionFlashCommandHandler`.
