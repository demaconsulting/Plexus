using Avalonia.Metadata;

// Lets XAML consumers write xmlns:plexus="https://github.com/demaconsulting/plexus" and use
// <plexus:DiagramEditor/> without naming the CLR namespace or the assembly
[assembly: XmlnsDefinition("https://github.com/demaconsulting/plexus", "DemaConsulting.Plexus.Avalonia")]
[assembly: XmlnsPrefix("https://github.com/demaconsulting/plexus", "plexus")]
