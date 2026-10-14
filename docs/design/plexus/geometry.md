## Geometry Unit Design

![Plexus Structure](PlexusView.svg)

### Purpose

Geometry provides the small immutable value types that every other Plexus item uses to describe positions, extents,
boxes and colors in diagram coordinates: `Point`, `Size`, `Rect` and `Color`. It is a Software Unit that sits directly
under the Plexus system. Plexus defines its own primitives, instead of reusing a UI framework's, so the core library
stays free of any UI dependency and host code can import UI and Plexus namespaces together without type collisions.

### Data Model

All four types are `readonly record struct` values, so equality and hash codes are structural, instances can be shared
between threads, and no instance ever changes after creation.

- **Point** (`X`, `Y` as double): a location. X grows to the right, Y grows downward. `Point.Zero` is the origin.
- **Size** (`Width`, `Height` as double): an extent. `Size.Empty` is 0 by 0. Kept separate from `Point` so that
  signatures state whether they take a location or an extent.
- **Rect** (`X`, `Y`, `Width`, `Height` as double): an axis-aligned box stored as top-left corner plus extent. A second
  constructor takes a `Point` and a `Size`. Edges (`Left`, `Top`, `Right`, `Bottom`), `Position`, `Size` and `Center`
  are computed on demand, so the struct holds no redundant state. `IsEmpty` is true when the width or the height is
  zero or negative.
- **Color** (`R`, `G`, `B`, `A` as byte, alpha defaults to 255): a 32-bit RGBA color. Named values (`Transparent`,
  `Black`, `White`, `Gray`, `Red`, `Green`, `Blue`, `Orange`) and the factories `FromRgb` and `FromArgb` are provided.

Invariants: no type validates its numbers; callers that need finite values check them (for example DiagramPage does when
adding a node). The textual color form is `#RRGGBB` for opaque colors and `#AARRGGBB` otherwise, using uppercase
digits, so snapshots round-trip colors exactly.

### Key Methods

- **Point.Offset(dx, dy)** and the operators `+ (Point, Size)`, `- (Point, Point)` and `+ (Point, Point)`: component-wise
  arithmetic returning a new point. Subtraction yields a displacement stored in a point.
- **Rect.FromPoints(a, b)**: takes the minimum of both X values and both Y values as the corner and the absolute
  differences as the extent, so corners may be passed in any order.
- **Rect.Contains(Point)** and **Rect.Contains(Rect)**: edge-inclusive comparisons against `Left`, `Right`, `Top` and
  `Bottom`.
- **Rect.Intersects(other)**: true when the rectangles share at least one point, so touching rectangles intersect.
- **Rect.Union(other)**: the smallest rectangle covering both, from the extreme edges.
- **Rect.Inflate(amount)**, **Rect.Offset(dx, dy)**, **Rect.WithPosition(point)**: return modified copies; a negative
  inflate amount shrinks the rectangle.
- **Color.TryParse(text, out color)**: precondition none (null is allowed). It succeeds only when the text is `#`
  followed by exactly six or eight characters that parse as hexadecimal digits with `AllowHexSpecifier` (which rejects
  signs and white space). Six digits give alpha 255; eight digits take alpha from the high byte. On failure the color is
  the default value and the method returns false.
- **Color.Parse(text)**: calls `TryParse` and converts failure into an exception.
- **Color.ToString()**: formats `#RRGGBB` when alpha is 255, otherwise `#AARRGGBB`, with the invariant culture.

### Error Handling

Geometry has almost no failure paths. `Color.Parse` throws `ArgumentNullException` for null text and `FormatException`
(with the offending text in the message) for text that is not a valid color. `Color.TryParse` never throws and reports
failure through its return value, which is the form used for untrusted input such as snapshot and clipboard text. The
geometry operations themselves do no validation and propagate floating-point values (including NaN and infinity)
unchanged; rejecting non-finite values is the job of the calling model code.

### Dependencies

Geometry depends only on the .NET base class library (`System.Globalization` for culture-invariant parsing and
formatting). It has no dependency on any other Plexus item, which lets every other item depend on it.

### Callers

Every other Plexus item uses Geometry. The Model subsystem stores node, annotation and group bounds as `Rect` and
positions as `Point`. Types declares port type colors and default node sizes. Properties holds color property values.
Scene, Routing and Serialization compute with points and rectangles and read and write colors in text form. The
Avalonia and Rendering systems convert these values to and from their painter types.
