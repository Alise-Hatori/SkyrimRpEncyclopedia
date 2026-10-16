# Dark Skyrim — WPF resource package

This package is connected by `App.xaml` and uses no external icon fonts.  Icons are WPF `DrawingImage` resources with transparent backgrounds, so they scale cleanly at any DPI.

## Files

- `Colors.xaml` — palette, shared brushes, type families, radii.
- `Icons.xaml` — named 20px `DrawingImage` resources for every main navigation and toolbar command.
- `Controls.xaml` — stone texture brush, panel, navigation/action/button states, search and scrolling styles.
- `nordic-stone-texture.png` — generated dark-nordic stone texture, used subtly under the main content.
- `NordRunePatternBrush` and `NordFrostPatternBrush` — DPI-independent, seamless vector texture tiles.
- `ThemePreview.xaml` — a standalone visual reference / copyable control samples.
- `ThemeDefaults.xaml` — implicit control styles deliberately imported into regular windows/views, but never the overlay.

## Use in another WPF window

`App.xaml` already merges all three dictionaries.  Apply a shared style, for example:

```xml
<Button Style="{StaticResource NordActionButton}">Open</Button>
<TextBox Style="{StaticResource NordSearchBox}" />
<Image Source="{StaticResource IconSearch}" Width="20" Height="20" />
```

The navigation icon layout is intentionally explicit in `MainWindow.xaml`, allowing a future screen to pick only the icons it needs. `Tag` stores a category's canonical name; it is independent of the displayed icon/layout.

## Palette

`NordVoid` is the window background; `NordStone`/`NordPanel` are the structural layers; `NordSilver` is standard text/icon ink; `NordBlue` is the focus/selected accent. The styles define Normal, Hover, Pressed, Disabled and Selected triggers.

## Reverting

The earlier interface was not overwritten with a backup because no existing asset file was replaced. To disconnect the theme, remove the three merged dictionaries from `App.xaml` and restore the prior `MainWindow.xaml` from source control or history.
