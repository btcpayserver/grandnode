
namespace Payments.BTCPayServer.Models
{
    public struct PaymentDataModel
    {
        public string CurrencyCode;
        public decimal Amount;
        public string OrderID;
        public string StoreID;
        public string CustomerID;
        public string Description;
        public string BuyerEmail;
        public string BuyerName;
        public string RedirectionURL;
        public string Lang;
        public string OrderUrl { get; set; }
    }
}