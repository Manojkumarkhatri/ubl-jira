namespace Upms.Domain.Common;

/// <summary>The type of a board column (a project status): "to do", "in progress" or "done" (research
/// R11). Shared by the Projects and Work modules and by later portfolio rollups.</summary>
public enum StatusCategory
{
    ToDo,
    InProgress,
    Done,
}
