namespace Upms.Application.Common;

/// <summary>A 1-based page request; every list is paged (constitution, performance baseline).</summary>
public sealed record PageRequest(int Page = 1, int PageSize = PageRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    public static PageRequest First { get; } = new();

    /// <summary>The same request with its values clamped to the allowed ranges.</summary>
    public PageRequest Normalized() => new(Math.Max(1, Page), Math.Clamp(PageSize, 1, MaxPageSize));

    public int Skip => (Math.Max(1, Page) - 1) * Math.Clamp(PageSize, 1, MaxPageSize);
}

/// <summary>One page of a list.</summary>
public sealed record Page<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize)
{
    public int PageCount => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasMore => PageNumber < PageCount;

    public static Page<T> Empty(PageRequest request) => new([], 0, request.Page, request.PageSize);
}
