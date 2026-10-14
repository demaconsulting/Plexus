## Avalonia

### Purpose

Avalonia is the cross-platform UI framework used by the PlexusAvalonia system to host the interactive diagram editor
control and by the PlexusAvaloniaPropertyGrid system for its controls. It was chosen because it runs on Windows, Linux
and macOS, supports custom-drawn controls, and offers a
headless platform (Avalonia.Headless) that allows the editor to be tested automatically without a display.

### Features Used

- Custom control base classes, styled and direct properties, and the drawing context used to paint the scene
- Pointer, mouse wheel and keyboard input events, focus handling and routed events
- Text formatting and measurement
- Drag-and-drop and operating-system clipboard services
- UI automation peers, used to expose the editor to accessibility and test tooling
- Avalonia.Headless (tests only): a display-less platform for creating windows, running layout and simulating input

### Integration Pattern

Avalonia is consumed as a NuGet package reference of `DemaConsulting.Plexus.Avalonia` and of
`DemaConsulting.Plexus.Avalonia.PropertyGrid`; the core package and the rendering package do not reference it. The
`DiagramEditor` unit derives from an Avalonia control and renders on the UI
thread, `ScenePainter` converts scene primitives into drawing-context calls, `AvaloniaTextMeasurer` implements the
core text-measurement interface, and `DiagramClipboard` bridges to the Avalonia clipboard. The editor does not require
a particular theme or application setup beyond a running Avalonia application. In tests, the headless platform is
started once per test run by the test application class. The dependency version is managed through the project file
and updated by automated dependency updates.
