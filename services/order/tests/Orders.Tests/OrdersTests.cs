using Orders.Service.Domain;

namespace Orders.Tests;

public class OrdersTests
{
    private static readonly Guid User = Guid.NewGuid();

    private static (OrderStore Store, OrderView Order) MakeOrder()
    {
        var s = new OrderStore();
        var o = s.Create(User, [new OrderLine("a", "A", 2, 100m), new OrderLine("b", null, 1, 50m)]);
        return (s, o);
    }

    [Fact]
    public void Create_computes_total_and_starts_created()
    {
        var (_, o) = MakeOrder();
        Assert.Equal(250m, o.TotalAmount);
        Assert.Equal("Created", o.Status);
    }

    [Fact]
    public void Create_rejects_empty_and_invalid_items()
    {
        var s = new OrderStore();
        Assert.Throws<ArgumentException>(() => s.Create(User, []));
        Assert.Throws<ArgumentException>(() => s.Create(User, [new OrderLine("a", null, 0, 1m)]));
        Assert.Throws<ArgumentException>(() => s.Create(Guid.Empty, [new OrderLine("a", null, 1, 1m)]));
    }

    [Fact]
    public void Confirmed_only_after_payment_and_reservation_in_any_order()
    {
        var (s, o) = MakeOrder();
        Assert.Equal("Created", s.MarkReserved(o.Id).Order!.Status);
        var t = s.MarkPaid(o.Id);
        Assert.Equal("Confirmed", t.Order!.Status);
        Assert.True(t.Changed);
    }

    [Fact]
    public void Payment_failure_cancels_created_order_once()
    {
        var (s, o) = MakeOrder();
        var first = s.Fail(o.Id, "payment_failed");
        var second = s.Fail(o.Id, "payment_failed");
        Assert.True(first.Changed);
        Assert.Equal("Created", first.PreviousStatus);
        Assert.False(second.Changed);
    }

    [Fact]
    public void Failure_after_confirmation_does_not_cancel()
    {
        var (s, o) = MakeOrder();
        s.MarkPaid(o.Id); s.MarkReserved(o.Id);
        Assert.False(s.Fail(o.Id, "late").Changed);
        Assert.Equal("Confirmed", s.Get(o.Id)!.Status);
    }

    [Fact]
    public void Manual_cancel_works_from_confirmed_and_is_idempotent()
    {
        var (s, o) = MakeOrder();
        s.MarkPaid(o.Id); s.MarkReserved(o.Id);
        Assert.True(s.Cancel(o.Id, "user").Changed);
        Assert.False(s.Cancel(o.Id, "user").Changed);
    }

    [Fact]
    public void Unknown_order_returns_null()
    {
        var s = new OrderStore();
        Assert.Null(s.MarkPaid(Guid.NewGuid()).Order);
        Assert.Null(s.Cancel(Guid.NewGuid(), "x").Order);
    }
}
