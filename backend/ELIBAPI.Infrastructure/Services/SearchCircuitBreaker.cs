namespace ELIBAPI.Infrastructure.Services;

/// <summary>Circuit breaker cho tìm kiếm OPAC ES→DB (Đợt 9) — state in-memory, singleton, KHÔNG đồng bộ
/// giữa nhiều instance API (chấp nhận có chủ đích, giống ELIB-LRC — mỗi instance tự học độc lập).
/// Closed → Open (sau đủ số lần lỗi liên tiếp) → HalfOpen (sau khi hết thời gian mở, thử lại đúng 1 request)
/// → Closed (nếu thử thành công) hoặc Open lại (nếu vẫn lỗi).</summary>
public class SearchCircuitBreaker
{
    private readonly object _lock = new();
    private int _consecutiveFailures;
    private DateTime? _openUntil;
    private bool _halfOpenTrialInFlight;

    public bool ShouldTryElastic(int failureThreshold, TimeSpan openDuration)
    {
        lock (_lock)
        {
            if (_openUntil == null) return true; // Closed

            if (DateTime.UtcNow < _openUntil) return false; // Open — vẫn trong thời gian chờ

            // Hết thời gian mở → HalfOpen: chỉ cho đúng 1 request thử lại tại 1 thời điểm.
            if (_halfOpenTrialInFlight) return false;
            _halfOpenTrialInFlight = true;
            return true;
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            _openUntil = null;
            _halfOpenTrialInFlight = false;
        }
    }

    public void RecordFailure(int failureThreshold, TimeSpan openDuration)
    {
        lock (_lock)
        {
            _halfOpenTrialInFlight = false;
            _consecutiveFailures++;
            if (_consecutiveFailures >= failureThreshold)
                _openUntil = DateTime.UtcNow.Add(openDuration);
        }
    }
}
