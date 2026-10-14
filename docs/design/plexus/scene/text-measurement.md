### TextMeasurement Unit Design

![Scene Structure](SceneView.svg)

#### Purpose

Defines how text is measured for layout (`ITextMeasurer`) and supplies a font-free default (`ApproximateTextMeasurer`)
so scenes can be built, hit tested and tested without any painter or installed font.

#### Data Model

`ITextMeasurer` has one method, `Measure(text, style)`, returning a `Size` in diagram units. `ApproximateTextMeasurer`
is stateless and has constants only: width factors relative to the font size of 0.53 for ordinary glyphs, 0.28 for
narrow glyphs (space, `i`, `j`, `l`, `t`, `f`, `I`, `1` and common punctuation) and 0.72 for wide glyphs (`m`, `w`, `@`,
`%` and every upper-case letter), a line height factor of 1.25 and a bold factor of 1.08.

#### Key Methods

`ApproximateTextMeasurer.Measure(text, style)`: sums the width class of every character, scales the sum by the font
size (and by the bold factor for bold text) and returns it with a height of font size times the line height factor.
Empty text therefore has width zero and the usual line height. The class table is tuned for proportional sans-serif
faces; it is an estimate "to fool the eye" and painters supply exact measurers when metrics matter.

Contract for implementations: deterministic for a given text and style; text is a single line.

#### Error Handling

`Measure` throws `ArgumentNullException` for a null text or style. Unknown characters are treated as ordinary glyphs.

#### Dependencies

- `TextStyle` of the DiagramTheme unit (font size and weight)
- Geometry unit (`Size`)

#### Callers

- `NodeLayout` and `NodeGeometry` (label widths for anchors always use the approximate measurer)
- `SceneBuilder` as the default when the options name no measurer
- PlexusRendering (`CanvasTextMeasurer`) and PlexusAvalonia (`AvaloniaTextMeasurer`) implement the interface
