namespace IntervalPulling.Rest.Api.Services;

internal record CacheServiceResult<T>
{
    private CacheServiceResult(States state, T? entity)
    {
        State = state;
        Entity = entity;
    }

    public static CacheServiceResult<T> InProgress() => new(States.InProgress, default);
    public static CacheServiceResult<T> WithError() => new(States.Error, default);
    public static CacheServiceResult<T> InCache(T entity) => new(States.InCache, entity);

    public enum States
    {
        InProgress,
        InCache,
        Error
    }

    public States State { get; init; }
    public T? Entity { get; init; }
}
