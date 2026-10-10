namespace Elib.Contracts.Events.Platform;

/// <summary>
/// Một trang trạng thái hiện tại (đúng dạng event) trả qua API nội bộ /internal — service mới triển khai hoặc dựng lại bản sao
/// đọc lần lượt theo <see cref="Next"/> (khoá tăng dần; null = hết).
/// </summary>
public sealed record StatePage<T>(IReadOnlyList<T> Items, long? Next);
