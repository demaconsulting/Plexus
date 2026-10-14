namespace DemaConsulting.Plexus.Scene;

/// <summary>
///     Identifies what part of a scene lies under a point.
/// </summary>
public enum HitKind
{
    /// <summary>Nothing but empty canvas.</summary>
    Canvas,

    /// <summary>The body of a node.</summary>
    Node,

    /// <summary>The instance name drawn above a node.</summary>
    NodeName,

    /// <summary>A port of a node.</summary>
    Port,

    /// <summary>A wire; the segment index tells which segment.</summary>
    Wire,

    /// <summary>The handle at a bend of a wire; the bend index tells which bend. Only reported for a wire that is selected (<see cref="SceneOptions.Selection"/>); an unselected wire reports <see cref="Wire"/> instead.</summary>
    WireBend,

    /// <summary>The handle at the source (output) end of a wire. Only reported for a wire that is selected (<see cref="SceneOptions.Selection"/>).</summary>
    WireSourceHandle,

    /// <summary>The handle at the target (input) end of a wire. Only reported for a wire that is selected (<see cref="SceneOptions.Selection"/>).</summary>
    WireTargetHandle,

    /// <summary>The body of an annotation.</summary>
    Annotation,

    /// <summary>The resize handle in the corner of an annotation. Only reported for an annotation that is selected (<see cref="SceneOptions.Selection"/>).</summary>
    AnnotationResizeHandle,

    /// <summary>The border of an expanded group's frame.</summary>
    Group,

    /// <summary>The title bar of an expanded group.</summary>
    GroupHeader,

    /// <summary>The collapse/expand toggle of a group, expanded or collapsed.</summary>
    GroupCollapseToggle,

    /// <summary>A collapsed group's summary box.</summary>
    CollapsedGroup,
}
