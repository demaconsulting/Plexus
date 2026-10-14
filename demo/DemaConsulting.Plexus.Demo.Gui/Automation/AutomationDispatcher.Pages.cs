using System.Text.Json.Nodes;

namespace DemaConsulting.Plexus.Demo.Gui.Automation;

/// <content>
///     Page commands: <c>select-page</c>, <c>add-page</c>, <c>rename-page</c>, <c>delete-page</c>.
/// </content>
internal sealed partial class AutomationDispatcher
{
    /// <summary>
    ///     Registers the page commands.
    /// </summary>
    private void RegisterPageCommands()
    {
        Add("select-page", request =>
        {
            var page = Session.FindPage(AutomationJson.RequireString(request, "page"));
            Session.SelectPage(page);
            return DescribePage(page, Session.Diagram.Pages.IndexOf(page));
        });
        Add("add-page", request =>
        {
            var page = Session.AddPage(AutomationJson.OptionalString(request, "name"));
            return DescribePage(page, Session.Diagram.Pages.IndexOf(page));
        });
        Add("rename-page", request =>
        {
            var page = Session.FindPage(AutomationJson.RequireString(request, "page"));
            var name = AutomationJson.RequireString(request, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The page name must not be empty.");
            }

            page.Name = name.Trim();
            return DescribePage(page, Session.Diagram.Pages.IndexOf(page));
        });
        Add("delete-page", request =>
        {
            var page = Session.FindPage(AutomationJson.RequireString(request, "page"));
            var name = page.Name;
            Session.DeletePage(page);
            return new JsonObject { ["deleted"] = name, ["pages"] = Session.Diagram.Pages.Count };
        });
    }
}
