## SvgSkia Verification

This document provides the verification evidence for the SvgSkia (Svg.Controls.Skia.Avalonia) OTS software item.
Requirements for this OTS item are defined in the SvgSkia OTS Software Requirements document.

### Required Functionality

Svg.Controls.Skia.Avalonia converts SVG text into images that Avalonia can draw, and fails detectably for malformed
SVG. The PlexusAvalonia system uses it to draw host-supplied node icons.

### Verification Approach

No dedicated OTS test project is used. The `PlexusIcons` tests in the PlexusAvalonia test project load known-good and
known-bad SVG text through the library and assert on the outcome, so a passing test run proves the behavior required
by the requirements below. Each scenario names a test method that ReqStream matches in the TRX results.

### Test Scenarios

#### PlexusIcons_GetImage_ValidSvg_ReturnsImage

**Scenario**: A valid SVG icon is loaded.

**Expected**: A drawable image is returned. **Requirement coverage**: `Plexus-OTS-SvgSkia-Render`.

#### PlexusIcons_GetImage_SameIcon_ReturnsCachedInstance

**Scenario**: The same SVG icon is requested twice.

**Expected**: The second request returns the same image instance, proving the loaded image is reusable.
**Requirement coverage**: `Plexus-OTS-SvgSkia-Render`.

#### PlexusIcons_GetImage_BrokenSvg_ReturnsNullWithoutThrowing

**Scenario**: A malformed SVG icon is loaded.

**Expected**: No exception reaches the caller and no image is returned. **Requirement coverage**:
`Plexus-OTS-SvgSkia-Malformed`.

### Requirements Coverage

- **`Plexus-OTS-SvgSkia-Render`**: PlexusIcons_GetImage_ValidSvg_ReturnsImage,
  PlexusIcons_GetImage_SameIcon_ReturnsCachedInstance
- **`Plexus-OTS-SvgSkia-Malformed`**: PlexusIcons_GetImage_BrokenSvg_ReturnsNullWithoutThrowing
