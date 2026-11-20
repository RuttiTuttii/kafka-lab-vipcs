using Users.Service.Domain;

namespace Users.Tests;

public class UsersTests
{
    [Fact]
    public void Register_and_get()
    {
        var s = new UserStore();
        var u = s.Register("Ann", "ann@x.ru");
        Assert.Equal("Ann", s.Get(u.Id)!.Name);
    }

    [Fact]
    public void Email_is_unique_ignoring_case()
    {
        var s = new UserStore();
        s.Register("Ann", "ann@x.ru");
        Assert.Throws<InvalidOperationException>(() => s.Register("Bob", "ANN@x.ru"));
    }

    [Fact]
    public void Invalid_input_is_rejected()
    {
        var s = new UserStore();
        Assert.Throws<ArgumentException>(() => s.Register("", "a@b.c"));
        Assert.Throws<ArgumentException>(() => s.Register("A", "no-at"));
    }

    [Fact]
    public void Same_order_is_counted_once()
    {
        var s = new UserStore();
        var u = s.Register("Ann", "ann@x.ru");
        var order = Guid.NewGuid();
        Assert.True(s.CountOrder(u.Id, order));
        Assert.False(s.CountOrder(u.Id, order));
        Assert.Equal(1, s.Get(u.Id)!.OrdersCount);
    }
}
