using System.Collections.Concurrent;

namespace PaymentService
{

    public sealed class PaymentStore(IConfiguration configuration)
    {
        private readonly ConcurrentDictionary<Guid, PaymentRecord> _paymentsByOrder = new();
        private readonly decimal _maxAmount = configuration.GetValue<decimal?>("PaymentRules:MaxSinglePaymentAmount") ?? 100_000m;

        public void Seed()
        {
            if (!_paymentsByOrder.IsEmpty) return;

            var demoOrderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var demoUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

            _paymentsByOrder[demoOrderId] = new(
                PaymentId: Guid.NewGuid(),
                OrderId: demoOrderId,
                UserId: demoUserId,
                Amount: 1500m,
                Status: "Completed",
                Reason: "Seeded demo payment",
                ProcessedAt: DateTime.UtcNow
            );
        }

        public IReadOnlyCollection<PaymentRecord> GetAll()
            => _paymentsByOrder.Values.ToArray();

        public PaymentRecord ProcessOrderPayment(OrderCreatedEvent order)
        {
            if (_paymentsByOrder.TryGetValue(order.OrderId, out var existing))
                return existing;

            bool success;
            string? reason;

            if (order.Amount <= 0)
            {
                success = false;
                reason = "Сумма заказа должна быть больше 0";
            }
            else if (order.Amount > _maxAmount)
            {
                success = false;
                reason = $"Превышен лимит разового платежа ({_maxAmount})";
            }
            else
            {
                success = Random.Shared.Next(1, 101) <= 90;
                reason = success 
                    ? "Оплата успешно подтверждена" 
                    : "Отказ платёжного шлюза (недостаточно средств)";
            }

            var record = new PaymentRecord(
                PaymentId: Guid.NewGuid(),
                OrderId: order.OrderId,
                UserId: order.UserId,
                Amount: order.Amount,
                Status: success ? "Completed" : "Failed",
                Reason: reason,
                ProcessedAt: DateTime.UtcNow
            );

            _paymentsByOrder[order.OrderId] = record;
            return record;
        }
    }
}