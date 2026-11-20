using Payments.Service.Domain;

namespace Payments.Tests;

public class PaymentsTests
{
    [Theory]
    [InlineData(100, 1000, true, null)]
    [InlineData(1000, 1000, true, null)]
    [InlineData(1001, 1000, false, "limit_exceeded")]
    [InlineData(0, 1000, false, "invalid_amount")]
    [InlineData(-5, 1000, false, "invalid_amount")]
    public void Policy_decides_by_amount_and_limit(double amount, double limit, bool ok, string? reason)
    {
        var r = PaymentPolicy.Evaluate((decimal)amount, (decimal)limit);
        Assert.Equal(ok, r.Ok);
        Assert.Equal(reason, r.Reason);
    }

    [Fact]
    public void Register_is_idempotent_by_order_id()
    {
        var s = new PaymentStore();
        var order = Guid.NewGuid();
        var (first, created1) = s.Register(order, Guid.NewGuid(), 100m);
        var (second, created2) = s.Register(order, Guid.NewGuid(), 999m);
        Assert.True(created1);
        Assert.False(created2);
        Assert.Equal(first.PaymentId, second.PaymentId);
        Assert.Equal(100m, second.Amount);
    }

    [Fact]
    public void Decision_is_final_and_happens_once()
    {
        var s = new PaymentStore();
        var order = Guid.NewGuid();
        s.Register(order, Guid.NewGuid(), 100m);
        Assert.True(s.Complete(order).Changed);
        Assert.False(s.Complete(order).Changed);
        Assert.False(s.Fail(order, "late").Changed);
        Assert.Equal("Completed", s.Get(order)!.Status);
    }

    [Fact]
    public void Completed_payment_gets_transaction_reference()
    {
        var s = new PaymentStore();
        var order = Guid.NewGuid();
        s.Register(order, Guid.NewGuid(), 100m);
        Assert.StartsWith("TXN-", s.Complete(order).Payment!.TransactionReference);
    }

    [Fact]
    public void Unknown_order_has_no_payment()
    {
        var s = new PaymentStore();
        Assert.Null(s.Complete(Guid.NewGuid()).Payment);
    }
}
