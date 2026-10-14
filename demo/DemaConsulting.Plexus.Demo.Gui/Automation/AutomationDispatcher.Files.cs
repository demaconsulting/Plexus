using System.Text.Json.Nodes;

namespace DemaConsulting.Plexus.Demo.Gui.Automation;

/// <content>
///     File commands: <c>save</c>, <c>open</c>, <c>new</c>, <c>render</c>. Automation never asks about unsaved changes.
/// </content>
internal sealed partial class AutomationDispatcher
{
    /// <summary>
    ///     Registers the file commands.
    /// </summary>
    private void RegisterFileCommands()
    {
        Add("save", request =>
        {
            var path = Session.Save(AutomationJson.OptionalString(request, "path"));
            return new JsonObject { ["path"] = path };
        });
        Add("open", request =>
        {
            var path = AutomationJson.RequireString(request, "path");
            Session.Open(path);
            return Info();
        });
        Add("new", _ =>
        {
            Session.New();
            return Info();
        });
        Add("render", request =>
        {
            var path = Path.GetFullPath(AutomationJson.RequireString(request, "path"));
            var page = ResolvePage(request);
            var scale = AutomationJson.OptionalNumber(request, "scale") ?? 1;
            if (scale <= 0)
            {
                throw new ArgumentException("The scale must be greater than zero.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            DemoSession.ExportPng(page, path, scale);
            return new JsonObject { ["path"] = path, ["page"] = page.Name, ["bytes"] = new FileInfo(path).Length };
        });
    }
}
