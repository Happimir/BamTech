namespace StargateAPI.Business.Dtos
{
    /// <summary>
    /// One page of a collection. TotalCount counts matching items across all pages; TotalPages is
    /// zero when nothing matches. A page beyond the last returns empty Items, not an error.
    /// </summary>
    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);
}
