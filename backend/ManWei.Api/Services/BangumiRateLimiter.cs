namespace ManWei.Api.Services;

public class BangumiRateLimiter
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private int _tokens = 120;
    private double _tokenAccumulator = 0;
    private DateTime _lastRefill = DateTime.UtcNow;

    public async Task<bool> WaitForTokenAsync(CancellationToken ct = default)
    {
        // 第一次尝试
        await _semaphore.WaitAsync(ct);
        try
        {
            RefillTokens();
            if (_tokens > 0) { _tokens--; return true; }
        }
        finally
        {
            _semaphore.Release();
        }

        // 无令牌：在锁外等待 500ms（补充一个令牌所需时间）
        try
        {
            await Task.Delay(500, ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        // 第二次尝试（最终机会）
        await _semaphore.WaitAsync(ct);
        try
        {
            RefillTokens();
            if (_tokens > 0) { _tokens--; return true; }
            return false;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void RefillTokens()
    {
        var elapsed = DateTime.UtcNow - _lastRefill;
        _tokenAccumulator += elapsed.TotalSeconds * 2;
        var toAdd = (int)_tokenAccumulator;
        if (toAdd > 0)
        {
            _tokens = Math.Min(120, _tokens + toAdd);
            _tokenAccumulator -= toAdd;
            _lastRefill = DateTime.UtcNow;
        }
    }
}