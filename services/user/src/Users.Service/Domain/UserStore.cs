namespace Users.Service.Domain;

public sealed record UserView(Guid Id, string Name, string Email, int OrdersCount, DateTimeOffset RegisteredAt, DateTimeOffset UpdatedAt);

// файл состояния: пользователи и уже посчитанные заказы
public sealed class UserState
{
    public List<UserEntry> Users { get; set; } = [];
    public List<Guid> CountedOrders { get; set; } = [];
}

public sealed class UserEntry
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int Orders { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// пользователи в памяти с записью в файл, email уникален без учёта регистра
public sealed class UserStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, UserEntry> _users = new();
    private readonly HashSet<Guid> _countedOrders = new();
    private readonly FileState<UserState> _file;

    // path = null отключает сохранение (тесты)
    public UserStore(string? path = null)
    {
        _file = new FileState<UserState>(path);
        var state = _file.Load();
        foreach (var u in state.Users) _users[u.Id] = u;
        foreach (var o in state.CountedOrders) _countedOrders.Add(o);
    }

    // true, если в файле уже было состояние: тестовых пользователей из конфига тогда не добавляем
    public bool HasData
    {
        get { lock (_gate) return _users.Count > 0; }
    }

    public UserView Register(string name, string email, Guid? id = null)
    {
        Validate(name, email);
        lock (_gate)
        {
            if (_users.Values.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"email {email} is already registered");
            var now = DateTimeOffset.UtcNow;
            var e = new UserEntry { Id = id ?? Guid.NewGuid(), Name = name.Trim(), Email = email.Trim(), RegisteredAt = now, UpdatedAt = now };
            _users[e.Id] = e;
            Persist();
            return View(e);
        }
    }

    public UserView? Get(Guid id)
    {
        lock (_gate) return _users.TryGetValue(id, out var e) ? View(e) : null;
    }

    public IReadOnlyList<UserView> List()
    {
        lock (_gate) return _users.Values.OrderBy(u => u.RegisteredAt).Select(View).ToList();
    }

    public UserView? Update(Guid id, string name, string email)
    {
        Validate(name, email);
        lock (_gate)
        {
            if (!_users.TryGetValue(id, out var e)) return null;
            if (_users.Values.Any(u => u.Id != id && string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"email {email} is already registered");
            e.Name = name.Trim(); e.Email = email.Trim(); e.UpdatedAt = DateTimeOffset.UtcNow;
            Persist();
            return View(e);
        }
    }

    // считает заказ пользователя один раз, повтор того же orderId игнорируется
    public bool CountOrder(Guid userId, Guid orderId)
    {
        lock (_gate)
        {
            if (!_users.TryGetValue(userId, out var e)) return false;
            if (!_countedOrders.Add(orderId)) return false;
            e.Orders++; e.UpdatedAt = DateTimeOffset.UtcNow;
            Persist();
            return true;
        }
    }

    private void Persist() => _file.Save(new UserState { Users = _users.Values.ToList(), CountedOrders = _countedOrders.ToList() });

    private static void Validate(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name is empty");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new ArgumentException("email is invalid");
    }

    private static UserView View(UserEntry e) => new(e.Id, e.Name, e.Email, e.Orders, e.RegisteredAt, e.UpdatedAt);
}
