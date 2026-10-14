## PlexusIcons Unit Verification Design

Scope: Converts host SVG icons to cached Avalonia images, tolerating malformed SVG.

### Verification Approach

The unit is tested directly with real SVG text and the real SVG library: the unit is a cache around the library's
conversion, so the tests check what comes out for good and bad input and that the cache returns the same object. The
tests are in `PlexusIconsTests.cs` and also provide the evidence for the Svg.Controls.Skia.Avalonia OTS requirements
(see _SvgSkia Verification_).

### Test Environment

The standard test runner plus the Avalonia headless platform started by the shared fixtures. No files are needed; the
SVG documents are embedded in the tests.

### Acceptance Criteria

A test passes when the returned image has the size declared by the SVG, repeated requests return the same instance, a
malformed icon returns null every time without an exception, and a null icon raises `ArgumentNullException`.

### Test Scenarios

- **PlexusIcons_GetImage_ValidSvg_ReturnsImage**: a valid 16 by 16 SVG yields an image of that size.
- **PlexusIcons_GetImage_SameIcon_ReturnsCachedInstance**: the same icon, and another icon with the same SVG, give the
  same image.
- **PlexusIcons_GetImage_BrokenSvg_ReturnsNullWithoutThrowing**: malformed text yields null, also on the second request
  (error path and cached failure).
- **PlexusIcons_GetImage_Null_Throws**: a null icon is rejected (error path).
