using System.Text.Json.Serialization;

namespace Orders.Service.Domain;

// файл состояния: список заказов
public sealed class OrderState
{
    public List<OrderEntry> Orders { get; set; } = [];
}

public sealed class OrderEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public List<OrderLine> Items { get; set; } = [];
    public decimal Total { get; set; }
    public string Currency { get; set; } = "RUB";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OrderStatus Status { get; set; }

    public bool Paid { get; set; }
    public bool Reserved { get; set; }
    public string? CancelReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// заказы в памяти с записью в файл после каждого изменения, все операции под одним замком
public sealed class OrderStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, OrderEntry> _orders = new();
    private readonly FileState<OrderState> _file;

    // path = null отключает сохранение (тесты)
    public OrderStore(string? path = null)
    {
        _file = new FileState<OrderState>(path);
        foreach (var e in _file.Load().Orders) _orders[e.Id] = e;
    }

    public OrderView Create(Guid userId, IReadOnlyList<OrderLine> items)
    {
        if (userId == Guid.Empty) throw new ArgumentException("userId is empty");
        if (items.Count == 0) throw new ArgumentException("items are empty");
        if (items.Any(i => string.IsNullOrWhiteSpace(i.Sku) || i.Quantity <= 0 || i.UnitPrice < 0))
            throw new ArgumentException("each item needs sku, quantity > 0 and unitPrice >= 0");

        var now = DateTimeOffset.UtcNow;
        var e = new OrderEntry
        {
            Id = Guid.NewGuid(), UserId = userId, Items = items.ToList(),
            Total = items.Sum(i => i.Quantity * i.UnitPrice), Status = OrderStatus.Created,
            CreatedAt = now, UpdatedAt = now,
        };
        lock (_gate) { _orders[e.Id] = e; Persist(); return View(e); }
    }

    // откат заказа, если событие order.created не удалось отправить
    public void Remove(Guid id)
    {
        lock (_gate) { if (_orders.Remove(id)) Persist(); }
    }

    public OrderView? Get(Guid id)
    {
        lock (_gate) return _orders.TryGetValue(id, out var e) ? View(e) : null;
    }

    public IReadOnlyList<OrderView> List()
    {
        lock (_gate) return _orders.Values.OrderBy(o => o.CreatedAt).Select(View).ToList();
    }

    // оплата прошла: если склад уже зарезервирован, заказ подтверждается
    public Transition MarkPaid(Guid id) => Update(id, e => { e.Paid = true; });

    // склад зарезервирован: если оплата уже прошла, заказ подтверждается
    public Transition MarkReserved(Guid id) => Update(id, e => { e.Reserved = true; });

    // отказ оплаты или склада отменяет только заказ в статусе Created
    public Transition Fail(Guid id, string reason)
    {
        lock (_gate)
        {
            if (!_orders.TryGetValue(id, out var e)) return new(null, false, null);
            if (e.Status != OrderStatus.Created) return new(View(e), false, null);
            return CancelLocked(e, reason);
        }
    }

    // ручная отмена: из Created и Confirmed, повторная отмена ничего не меняет
    public Transition Cancel(Guid id, string reason)
    {
        lock (_gate)
        {
            if (!_orders.TryGetValue(id, out var e)) return new(null, false, null);
            if (e.Status == OrderStatus.Cancelled) return new(View(e), false, null);
            return CancelLocked(e, reason);
        }
    }

    // ручная смена статуса на Confirmed, отмена идёт через Cancel
    public Transition Confirm(Guid id)
    {
        lock (_gate)
        {
            if (!_orders.TryGetValue(id, out var e)) return new(null, false, null);
            if (e.Status != OrderStatus.Created) return new(View(e), false, null);
            var prev = e.Status.ToString();
            e.Status = OrderStatus.Confirmed; e.UpdatedAt = DateTimeOffset.UtcNow;
            Persist();
            return new(View(e), true, prev);
        }
    }

    private Transition Update(Guid id, Action<OrderEntry> apply)
    {
        lock (_gate)
        {
            if (!_orders.TryGetValue(id, out var e)) return new(null, false, null);
            var prev = e.Status;
            apply(e);
            if (e.Status == OrderStatus.Created && e.Paid && e.Reserved) e.Status = OrderStatus.Confirmed;
            e.UpdatedAt = DateTimeOffset.UtcNow;
            Persist();
            return new(View(e), e.Status != prev, prev.ToString());
        }
    }

    private Transition CancelLocked(OrderEntry e, string reason)
    {
        var prev = e.Status.ToString();
        e.Status = OrderStatus.Cancelled; e.CancelReason = reason; e.UpdatedAt = DateTimeOffset.UtcNow;
        Persist();
        return new(View(e), true, prev);
    }

    private void Persist() => _file.Save(new OrderState { Orders = _orders.Values.ToList() });

    private static OrderView View(OrderEntry e) => new(
        e.Id, e.UserId, e.Items.ToList(), e.Total, e.Currency, e.Status.ToString(),
        e.Paid, e.Reserved, e.CancelReason, e.CreatedAt, e.UpdatedAt);
}
