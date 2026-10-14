### IconSource Unit Verification Design

This document describes how the IconSource unit (the SVG icon descriptor) is verified. Requirements are defined in the
IconSource Unit Requirements document.

#### Verification Approach

The unit tests use the real `IconSource` class and no mocks. File loading is verified with a real file written next to
the test binaries and deleted afterwards. Resource loading is verified with a small in-memory assembly stand-in that
serves one named resource, because the test project embeds no SVG resources; the same stand-in proves the missing
resource path. The tests are in `Types/IconSourceTests.cs` and `TypesAndGeometryTests.cs`.

#### Test Environment

No setup beyond the standard xUnit v3 test runner on .NET. The file tests need write access to the test output folder.
No external services are needed.

#### Acceptance Criteria

All IconSource unit tests pass: icons expose the supplied text, equal content gives equal keys and equality, different
content differs, loading from files and resources yields the content, and invalid input raises the documented
exceptions.

#### Test Scenarios

##### Creation from Text (`Plexus-Types-IconSource-FromSvg`)

- **IconSource_FromSvg_HasStableContentKey**: the text is exposed unchanged and a blank string is rejected.
- **IconSource_FromSvg_BlankText_ThrowsArgumentException**: null, empty and white-space text are rejected.

##### Content Key (`Plexus-Types-IconSource-ContentKey`)

- **IconSource_FromSvg_HasStableContentKey**: equal text gives equal keys; text that differs by one character differs.
- **IconSource_Key_HasPrefixAndHexDigest**: the key has the `svg:` prefix and 24 hexadecimal digits.

##### Equality (`Plexus-Types-IconSource-Equality`)

- **IconSource_FromSvg_HasStableContentKey**: equal icons have equal hash codes; different icons are not equal.
- **IconSource_Equality_FollowsContent**: equality works through both `Equals` overloads, null and other types are not
  equal, and a set holds one entry for equal icons.

##### Creation from a File (`Plexus-Types-IconSource-FromFile`)

- **IconSource_FromSvgFile_ExistingFile_LoadsContent**: the file content becomes the icon text.
- **IconSource_FromSvgFile_MissingFileOrBlankPath_Throws**: a missing file raises an I/O exception; blank and null
  paths raise argument exceptions.
- **IconSource_FromFileAndResource_LoadContentAndRejectMissing**: file loading and a missing resource in one scenario.

##### Creation from a Resource (`Plexus-Types-IconSource-FromResource`)

- **IconSource_FromSvgResource_ExistingResource_LoadsContent**: the resource text becomes the icon text.
- **IconSource_FromSvgResource_MissingResourceOrInvalidArguments_Throws**: the missing-resource message names the
  resource; a null assembly and a blank name are rejected.
- **IconSource_FromFileAndResource_LoadContentAndRejectMissing**: a resource that does not exist in a real assembly is
  rejected.
