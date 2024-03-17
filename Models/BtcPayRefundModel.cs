namespace Payments.BTCPayServer.Models
{
    public class BtcPayRefundModel
    {
        public string? name;
        public string? description;
        public string? paymentMethod;
        public string? refundVariant;
    }

    public class BtcPayRefundCustomModel : BtcPayRefundModel
    {
        public decimal customAmount;
        public string? customCurrency;
    }
}
