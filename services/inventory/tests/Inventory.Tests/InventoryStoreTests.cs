using Inventory.Service.Domain;

namespace Inventory.Tests;

public class InventoryStoreTests
{
    private static InventoryStore Make()
    {
        var s = new InventoryStore();
        s.Set("a", 10);
        s.Set("b", 1);
        return s;
    }

    [Fact]
    public void Reserve_decrements_stock_on_success()
    {
        var s = Make();
        var r = s.Reserve(Guid.NewGuid(), [("a", 3)]);
        Assert.True(r.Success);
        s.TryGet("a", out var left);
        Assert.Equal(7, left);
    }

    [Fact]
    public void Reserve_is_all_or_nothing()
    {
        var s = Make();
        var r = s.Reserve(Guid.NewGuid(), [("a", 3), ("b", 5)]);
        Assert.False(r.Success);
        Assert.Equal("insufficient_stock:b", r.Reason);
        s.TryGet("a", out var left);
        Assert.Equal(10, left);
    }

    [Fact]
    public void Reserve_is_idempotent_by_order_id()
    {
        var s = Make();
        var id = Guid.NewGuid();
        s.Reserve(id, [("a", 3)]);
        var again = s.Reserve(id, [("a", 3)]);
        Assert.True(again.Success);
        s.TryGet("a", out var left);
        Assert.Equal(7, left);
    }

    [Fact]
    public void Reserve_sums_duplicate_skus()
    {
        var s = Make();
        var r = s.Reserve(Guid.NewGuid(), [("a", 6), ("a", 6)]);
        Assert.False(r.Success);
    }

    [Fact]
    public void Reserve_rejects_unknown_sku_and_empty_order()
    {
        var s = Make();
        Assert.Equal("unknown_sku:x", s.Reserve(Guid.NewGuid(), [("x", 1)]).Reason);
        Assert.Equal("empty_order", s.Reserve(Guid.NewGuid(), []).Reason);
    }

    [Fact]
    public void Set_rejects_negative_quantity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Make().Set("a", -1));
    }
}
