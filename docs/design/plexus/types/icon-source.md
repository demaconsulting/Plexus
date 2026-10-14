### IconSource Unit Design

![Types Structure](TypesView.svg)

#### Purpose

IconSource is the Software Unit that describes the picture a node type can show on its nodes. A host supplies the
picture as SVG, and IconSource keeps the SVG text together with a stable content key that painters use to cache decoded
images. It does not decode or draw anything, so the core library stays free of graphics dependencies.

#### Data Model

`IconSource` is a sealed, immutable class implementing `IEquatable<IconSource>`.

- **SvgText**: the SVG document text exactly as supplied or read.
- **Key**: `"svg:"` followed by 24 uppercase hexadecimal digits, which are the first 12 bytes of the SHA-256 hash of
  the UTF-8 bytes of the text. It is computed once in the private constructor.

Invariants: the text is never null, empty or white space; equal text always gives an equal key. Instances are created
only through the three factory methods and never change afterwards, so they are safe to share between node types and
threads. Equality and the hash code are based only on the key.

#### Key Methods

- **FromSvg(svg)**: precondition is text that is not null, empty or white space. Creates the icon and its key.
- **FromSvgFile(path)**: precondition is a non-blank path to a readable file. Reads the whole file immediately as text
  and then behaves like `FromSvg`, so later changes to the file do not affect the icon.
- **FromSvgResource(assembly, resourceName)**: preconditions are a non-null assembly and a non-blank name. Opens the
  manifest resource stream, reads it as text and behaves like `FromSvg`. A resource that does not exist is reported
  immediately.
- **Equals(IconSource)**, **Equals(object)** and **GetHashCode()**: compare and hash by key only.

#### Error Handling

Invalid arguments are reported with `ArgumentException` (blank text, blank path or name, or a resource that is not
found; the message names the resource and assembly) and `ArgumentNullException` (null assembly). A file that cannot be
read surfaces the original `IOException` family exception from the file system. Nothing is caught or retried; the host
learns of a bad icon at the point it creates it, instead of seeing a blank icon later. Malformed SVG content is not
detected here; the painters treat undecodable content as a missing icon.

#### Dependencies

Uses only the .NET base class library: SHA-256 hashing, UTF-8 encoding, file reading and assembly resource access. It
depends on no other Plexus item.

#### Callers

`NodeType.Icon` (see NodeTypeRegistry Design) holds an optional icon. The Scene subsystem reads the icon when laying
out and building the node and emits an icon primitive that carries it. The Rendering and Avalonia painters decode the
SVG and cache the result by `Key`. Hosts create icons when they declare node types and may also use `SvgText` for
palettes.
