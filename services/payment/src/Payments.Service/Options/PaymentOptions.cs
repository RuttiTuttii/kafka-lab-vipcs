namespace Payments.Service.Options;

// секция Payment в appsettings
public sealed class PaymentOptions
{
    // auto: решение принимает сервис, manual: платёж ждёт confirm или decline через API
    public string Mode { get; init; } = "auto";

    // суммы больше лимита отклоняются
    public decimal Limit { get; init; } = 50000m;

    // задержка имитации шлюза, мс
    public int DelayMs { get; init; } = 300;
}
