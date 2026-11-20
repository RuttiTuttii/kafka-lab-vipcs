using System.Text.Json.Serialization;

namespace Payments.Service.Domain;

// файл состояния: платежи по заказам
public sealed class PaymentState
{
    public List<PaymentEntry> Payments { get; set; } = [];
}

public sealed class PaymentEntry
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PaymentStatus Status { get; set; }

    public string? Reason { get; set; }
    public string? Reference { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// платежи по orderId с записью в файл: повторный order.created не создаёт второй платёж, даже после перезапуска
public sealed class PaymentStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, PaymentEntry> _byOrder = new();
    private readonly FileState<PaymentState> _file;

    // path = null отключает сохранение (тесты)
    public PaymentStore(string? path = null)
    {
        _file = new FileState<PaymentState>(path);
        foreach (var e in _file.Load().Payments) _byOrder[e.OrderId] = e;
    }

    // возвращает (платёж, true) если он создан сейчас, (платёж, false) если уже был
    public (PaymentView Payment, bool Created) Register(Guid orderId, Guid userId, decimal amount)
    {
        lock (_gate)
        {
            if (_byOrder.TryGetValue(orderId, out var known)) return (View(known), false);
            var now = DateTimeOffset.UtcNow;
            var e = new PaymentEntry { PaymentId = Guid.NewGuid(), OrderId = orderId, UserId = userId, Amount = amount, Status = PaymentStatus.Pending, CreatedAt = now, UpdatedAt = now };
            _byOrder[orderId] = e;
            Persist();
            return (View(e), true);
        }
    }

    public PaymentView? Get(Guid orderId)
    {
        lock (_gate) return _byOrder.TryGetValue(orderId, out var e) ? View(e) : null;
    }

    public IReadOnlyList<PaymentView> List()
    {
        lock (_gate) return _byOrder.Values.OrderBy(p => p.CreatedAt).Select(View).ToList();
    }

    public Decision Complete(Guid orderId)
    {
        lock (_gate)
        {
            if (!_byOrder.TryGetValue(orderId, out var e)) return new(null, false);
            if (e.Status != PaymentStatus.Pending) return new(View(e), false);
            e.Status = PaymentStatus.Completed;
            e.Reference = "TXN-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd") + "-" + e.PaymentId.ToString("N")[..6].ToUpperInvariant();
            e.UpdatedAt = DateTimeOffset.UtcNow;
            Persist();
            return new(View(e), true);
        }
    }

    public Decision Fail(Guid orderId, string reason)
    {
        lock (_gate)
        {
            if (!_byOrder.TryGetValue(orderId, out var e)) return new(null, false);
            if (e.Status != PaymentStatus.Pending) return new(View(e), false);
            e.Status = PaymentStatus.Failed; e.Reason = reason; e.UpdatedAt = DateTimeOffset.UtcNow;
            Persist();
            return new(View(e), true);
        }
    }

    private void Persist() => _file.Save(new PaymentState { Payments = _byOrder.Values.ToList() });

    private static PaymentView View(PaymentEntry e) => new(
        e.PaymentId, e.OrderId, e.UserId, e.Amount, e.Status.ToString(), e.Reason, e.Reference, e.CreatedAt, e.UpdatedAt);
}
