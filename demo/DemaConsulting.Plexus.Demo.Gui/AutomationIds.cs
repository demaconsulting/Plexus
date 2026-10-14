using DemaConsulting.Plexus.Avalonia;

namespace DemaConsulting.Plexus.Demo.Gui;

/// <summary>
///     The AutomationId vocabulary of the demo: every interactive control carries one so UI
///     automation (the Puppet) can find it without relying on captions or tree position.
/// </summary>
/// <remarks>
///     Scheme: <c>Area_Item[_Detail]</c> with underscores between the parts and PascalCase words.
///     Menus are <c>Menu_{Menu}_{Item}</c>, toolbar buttons are <c>Toolbar_{Command}</c>, the properties dialog and panel use the ids of the library's property grid (<c>PropertyGridDialog_*</c>, <c>PropertyGrid_Row_*</c>), other dialogs are <c>Dialog_*</c>,
///     context menu entries are <c>Context_{Target}_{Item}</c>. Dynamic items append the node type's
///     short name (<c>Mixer</c> for <c>demo.audio.Mixer</c>), a page index or a sample name.
///     The full list is documented in the demo's README.
/// </remarks>
internal static class AutomationIds
{
    /// <summary>The main window.</summary>
    public const string MainWindow = "Main_Window";

    /// <summary>The menu bar.</summary>
    public const string MenuBar = "Menu_Bar";

    /// <summary>The toolbar under the menu bar (shown or hidden by View &gt; Show Toolbar).</summary>
    public const string Toolbar = "Toolbar";

    /// <summary>The node palette (the library's <c>NodePalette</c> control, which uses this id by default).</summary>
    public const string Palette = "NodePalette";

    /// <summary>The splitter between the palette and the canvas; drag it to resize the palette.</summary>
    public const string PaletteSplitter = "Main_PaletteSplitter";

    /// <summary>The palette's built-in search box (the library's <c>NodePalette</c> assigns this id).</summary>
    public const string PaletteFilter = NodePalette.FilterAutomationId;

    /// <summary>The page tab strip.</summary>
    public const string PagesTabs = "Pages_Tabs";

    /// <summary>The diagram editor control.</summary>
    public const string Editor = "Editor";

    /// <summary>The status bar.</summary>
    public const string StatusBar = "Status_Bar";

    /// <summary>The status message.</summary>
    public const string StatusText = "Status_Text";

    /// <summary>The selection count in the status bar.</summary>
    public const string StatusSelection = "Status_Selection";

    /// <summary>The zoom percentage in the status bar.</summary>
    public const string StatusZoom = "Status_Zoom";

    /// <summary>The dirty marker in the status bar.</summary>
    public const string StatusDirty = "Status_Dirty";

    /// <summary>The properties panel docked on the right of the window (shown by View &gt; Properties Panel).</summary>
    public const string PropertiesPanel = "Properties_Panel";

    /// <summary>The splitter between the editor and the properties panel; drag it to resize the panel.</summary>
    public const string PropertiesPanelSplitter = "Main_PropertiesPanelSplitter";

    /// <summary>The properties dialog window (the library's <c>PropertyGridDialog</c> uses this id).</summary>
    public const string PropertiesDialog = "PropertyGridDialog";

    /// <summary>The OK button of the properties dialog.</summary>
    public const string PropertiesOk = "PropertyGridDialog_OK";

    /// <summary>The Cancel button of the properties dialog.</summary>
    public const string PropertiesCancel = "PropertyGridDialog_Cancel";

    /// <summary>The text of the properties dialog that tells why OK was refused.</summary>
    public const string PropertiesStatus = "PropertyGridDialog_Status";
    /// <summary>The prompt dialog window.</summary>
    public const string PromptDialog = "Dialog_Prompt";

    /// <summary>The text box of the prompt dialog.</summary>
    public const string PromptText = "Dialog_Prompt_Text";

    /// <summary>The OK button of the prompt dialog.</summary>
    public const string PromptOk = "Dialog_Prompt_Ok";

    /// <summary>The Cancel button of the prompt dialog.</summary>
    public const string PromptCancel = "Dialog_Prompt_Cancel";

    /// <summary>The message dialog window.</summary>
    public const string MessageDialog = "Dialog_Message";

    /// <summary>The text of the message dialog.</summary>
    public const string MessageText = "Dialog_Message_Text";

    /// <summary>The context menu.</summary>
    public const string ContextMenu = "Context_Menu";

    /// <summary>
    ///     Gets the short name of a node type key: the part after the last dot.
    /// </summary>
    /// <param name="typeKey">The node type key, for example <c>demo.audio.Mixer</c>.</param>
    /// <returns>The short name, for example <c>Mixer</c>.</returns>
    public static string ShortName(string typeKey) => typeKey[(typeKey.LastIndexOf('.') + 1)..];

    /// <summary>
    ///     Gets the id of a menu or menu item: <c>Menu_{path}</c>.
    /// </summary>
    /// <param name="path">The underscore-separated path, for example <c>File_Save</c>.</param>
    /// <returns>The AutomationId.</returns>
    public static string Menu(string path) => $"Menu_{path}";

    /// <summary>
    ///     Gets the id of a toolbar button: <c>Toolbar_{command}</c>.
    /// </summary>
    /// <param name="command">The command name, which is the menu item's name without its menu, for example <c>Undo</c> for <c>Menu_Edit_Undo</c>.</param>
    /// <returns>The AutomationId.</returns>
    public static string ToolbarButton(string command) => $"Toolbar_{command}";

    /// <summary>
    ///     Gets the id of a palette entry, which the library's <c>NodePalette</c> assigns (<see cref="NodePalette.GetAutomationId(string)"/>).
    /// </summary>
    /// <param name="typeKey">The node type key, for example <c>demo.audio.Mixer</c>.</param>
    /// <returns>The AutomationId, for example <c>NodePalette_demo_audio_Mixer</c>.</returns>
    public static string PaletteItem(string typeKey) => NodePalette.GetAutomationId(typeKey);

    /// <summary>
    ///     Gets the id of a page tab.
    /// </summary>
    /// <param name="index">The zero-based page index.</param>
    /// <returns>The AutomationId.</returns>
    public static string PageTab(int index) => $"Page_Tab_{index}";

    /// <summary>
    ///     Gets the id of the row of a property in the properties dialog or the panel (the library's <c>PropertyGrid</c> assigns it).
    /// </summary>
    /// <param name="path">The property path: <c>Name</c>, a property name, a host data property such as <c>Priority</c>, or a nested path such as <c>Contact.Email</c> or <c>Checklist[0].Text</c>.</param>
    /// <returns>The AutomationId, for example <c>PropertyGrid_Row_Name</c>.</returns>
    public static string PropertiesRow(string path) => $"PropertyGrid_Row_{path}";

    /// <summary>
    ///     Gets the id of the editor control inside the row of a property in the properties dialog or the panel.
    /// </summary>
    /// <param name="path">The property path.</param>
    /// <returns>The AutomationId, for example <c>PropertyGrid_Editor_Name</c>.</returns>
    public static string PropertiesEditor(string path) => $"PropertyGrid_Editor_{path}";
    /// <summary>
    ///     Gets the id of a message dialog button.
    /// </summary>
    /// <param name="button">The button name: Yes, No, Cancel or Ok.</param>
    /// <returns>The AutomationId.</returns>
    public static string MessageButton(string button) => $"Dialog_Message_{button}";

    /// <summary>
    ///     Gets the id of a context menu entry.
    /// </summary>
    /// <param name="path">The underscore-separated path, for example <c>Node_Properties</c>.</param>
    /// <returns>The AutomationId.</returns>
    public static string Context(string path) => $"Context_{path}";
}
