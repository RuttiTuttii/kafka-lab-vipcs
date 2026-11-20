namespace Inventory.Service.Domain;

public sealed record ReserveResult(bool Success, string? Reason);

// склад в памяти: все операции под одним замком, резерв атомарный
public sealed class InventoryStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _stock = new(StringComparer.Ordinal);

    // результат по orderId нужен для идемпотентности: повтор события отдаёт тот же ответ
    private readonly Dictionary<Guid, ReserveResult> _outcomes = new();

    public IReadOnlyDictionary<string, int> Snapshot()
    {
        lock (_gate) return new Dictionary<string, int>(_stock);
    }

    public bool TryGet(string sku, out int quantity)
    {
        lock (_gate) return _stock.TryGetValue(sku, out quantity);
    }

    // задаёт остаток целиком, создаёт товар если его не было
    public void Set(string sku, int quantity)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("sku is empty");
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity), "quantity must be >= 0");
        lock (_gate) _stock[sku] = quantity;
    }

    public ReserveResult Reserve(Guid orderId, IReadOnlyCollection<(string Sku, int Quantity)> items)
    {
        lock (_gate)
        {
            if (_outcomes.TryGetValue(orderId, out var known)) return known;

            var result = TryReserveLocked(items);
            _outcomes[orderId] = result;
            return result;
        }
    }

    private ReserveResult TryReserveLocked(IReadOnlyCollection<(string Sku, int Quantity)> items)
    {
        if (items.Count == 0) return new(false, "empty_order");
        if (items.Any(i => i.Quantity <= 0)) return new(false, "invalid_quantity");

        // складываем одинаковые sku, чтобы проверка шла по суммарному количеству
        var need = items.GroupBy(i => i.Sku).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        foreach (var (sku, qty) in need)
        {
            if (!_stock.TryGetValue(sku, out var have)) return new(false, $"unknown_sku:{sku}");
            if (have < qty) return new(false, $"insufficient_stock:{sku}");
        }

        // проверили всё, только теперь списываем
        foreach (var (sku, qty) in need) _stock[sku] -= qty;
        return new(true, null);
    }
}
