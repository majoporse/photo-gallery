using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Gallery.Api.Controllers;

/// <summary>
/// Paging is rejected rather than clamped: a client that asks for page 0 has a
/// bug, and silently answering page 1 hides it. The messages name the query
/// keys the way the contract documents them.
/// </summary>
public sealed class PagingQuery
{
    public const int DefaultPageSize = 24;

    [FromQuery(Name = "page")]
    [Range(1, int.MaxValue, ErrorMessage = "page must be 1 or greater.")]
    public int? Page { get; set; }

    [FromQuery(Name = "pageSize")]
    [Range(1, 100, ErrorMessage = "pageSize must be between 1 and 100.")]
    public int? PageSize { get; set; }

    public (int Page, int PageSize) Resolve() => (Page ?? 1, PageSize ?? DefaultPageSize);
}
