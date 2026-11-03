using Telegram.Bot.Exceptions;

namespace RemindMe.Bot;

/// <summary>
/// Retries transient Telegram Bot API failures (5xx, 429, network timeouts).
/// </summary>
/// <remarks>
/// <para>
/// Telegram occasionally returns <c>502 Bad Gateway</c> (and other 5xx) from
/// its edge, and rate-limits callers with <c>429 Too Many Requests</c> plus a
/// <c>retry_after</c> hint. Both are transient: the correct response is to
/// wait and retry, not to crash the process or drop the work item.
/// </para>
/// <para>
/// Only exceptions that are known to be transient are retried. Anything else
/// (for example <c>401 Unauthorized</c>, <c>400 Bad Request</c>, programming
/// errors) is rethrown immediately so it surfaces in logs instead of being
/// silently retried.
/// </para>
/// <para>
/// Backoff is exponential with jitter, capped at 30 seconds. For 429, the
/// delay respects the server-provided <c>RetryAfter</c> hint (in seconds).
/// </para>
/// </remarks>
public static class TelegramApiRetry
{
    /// <summary>
    /// Executes <paramref name="action"/> and retries it on transient failures.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">Human-readable name used in log messages.</param>
    /// <param name="action">The Telegram call to execute.</param>
    /// <param name="maxAttempts">Total attempts including the first one. Must be &gt;= 1.</param>
    /// <param name="ct">Cancellation token for the delay between attempts.</param>
    /// <returns>The result of a successful attempt.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxAttempts"/> is less than 1.
    /// </exception>
    /// <exception cref="Exception">
    /// The last exception thrown by <paramref name="action"/> when it is either
    /// non-transient or all attempts have been exhausted.
    /// </exception>
    public static async Task<T> ExecuteAsync<T>(
        string operation,
        Func<CancellationToken, Task<T>> action,
        int maxAttempts,
        CancellationToken ct)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Must be >= 1.");

        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                return await action(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxAttempts)
            {
                var delay = ComputeDelay(ex, attempt);
                Console.WriteLine(
                    $"[WARN] {operation} transient error (attempt {attempt}/{maxAttempts}): " +
                    $"{ex.GetType().Name}: {ex.Message}. Retrying in {delay.TotalSeconds:0.#}s...");
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Non-generic overload for operations that return no value.
    /// </summary>
    /// <param name="operation">Human-readable name used in log messages.</param>
    /// <param name="action">The Telegram call to execute.</param>
    /// <param name="maxAttempts">Total attempts including the first one. Must be &gt;= 1.</param>
    /// <param name="ct">Cancellation token for the delay between attempts.</param>
    /// <returns>A task that completes when the operation succeeds or attempts are exhausted.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxAttempts"/> is less than 1.
    /// </exception>
    /// <exception cref="Exception">
    /// The last exception thrown by <paramref name="action"/> when it is either
    /// non-transient or all attempts have been exhausted.
    /// </exception>
    public static Task ExecuteAsync(
        string operation,
        Func<CancellationToken, Task> action,
        int maxAttempts,
        CancellationToken ct) =>
        ExecuteAsync<object?>(
            operation,
            async c => { await action(c).ConfigureAwait(false); return null; },
            maxAttempts,
            ct);

    /// <summary>
    /// Returns <c>true</c> if the exception represents a transient Telegram/network failure.
    /// </summary>
    /// <param name="ex">The exception to classify.</param>
    /// <returns>
    /// <c>true</c> for 5xx and 429 Telegram errors and for network-level failures
    /// (<see cref="HttpRequestException"/>, HTTP timeouts); otherwise <c>false</c>.
    /// </returns>
    private static bool IsTransient(Exception ex) => ex switch
    {
        // Telegram-side errors. 5xx and 429 are transient; other 4xx are not.
        ApiRequestException api => api.ErrorCode is >= 500 or 429,

        // Network-level failures.
        HttpRequestException => true,
        TaskCanceledException => true,  // typically an HTTP timeout
        TimeoutException => true,

        _ => false,
    };

    /// <summary>
    /// Computes the delay before the next attempt.
    /// </summary>
    /// <param name="ex">The transient exception that triggered the retry.</param>
    /// <param name="attempt">The 1-based number of the attempt that just failed.</param>
    /// <returns>
    /// The server-provided <c>RetryAfter</c> when the failure is a 429 with a
    /// positive hint; otherwise an exponential backoff with jitter, capped at 30 seconds.
    /// </returns>
    private static TimeSpan ComputeDelay(Exception ex, int attempt)
    {
        // For 429, honour Telegram's retry_after hint (int seconds in Telegram.Bot 22.10.3.3).
        if (ex is ApiRequestException { ErrorCode: 429 } rate
            && rate.Parameters!.RetryAfter is { } retryAfterSeconds
            && retryAfterSeconds > 0)
        {
            return TimeSpan.FromSeconds(retryAfterSeconds);
        }

        // Exponential backoff with jitter: 2s, 4s, 8s, 16s, 30s... capped at 30s.
        var seconds = Math.Min(Math.Pow(2, attempt), 30);
        var jitter = Random.Shared.NextDouble();
        return TimeSpan.FromSeconds(seconds + jitter);
    }
}