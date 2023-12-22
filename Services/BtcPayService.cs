using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using Grand.Business.Core.Interfaces.Checkout.Orders;
using Grand.Business.Core.Utilities.Checkout;
using Grand.Domain.Orders;
using Grand.Domain.Payments;
using Newtonsoft.Json.Linq;
using Payments.BTCPayServer.Models;
using System.Security.Cryptography;

namespace Payments.BTCPayServer.Services
{
    public class BtcPayService
    {

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOrderService _orderService;

        public BtcPayService(IOrderService orderService, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _orderService = orderService;
        }

        public static bool CheckSecretKey(string key, string message, string signature)
        {
            var msgBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(message));
            string hashString = string.Empty;
            foreach (byte x in msgBytes)
            {
                hashString += String.Format("{0:x2}", x);
            }
            return (hashString == signature);
        }

        public async Task<InvoiceData> CreateInvoice(BtcPaySettings settings, PaymentDataModel paymentData)
        {
            var client = GetClient(settings);
            var req = new CreateInvoiceRequest() {
                Currency = paymentData.CurrencyCode,
                Amount = paymentData.Amount,
                Checkout = new InvoiceDataBase.CheckoutOptions() {
                    DefaultLanguage = paymentData.Lang,
                    RedirectURL = paymentData.RedirectionURL,
                    RedirectAutomatically = true,
                    RequiresRefundEmail = false
                },
                Metadata = JObject.FromObject(new
                {
                    buyerEmail = paymentData.BuyerEmail,
                    buyerName = paymentData.BuyerName,
                    orderId = paymentData.OrderID,
                    orderUrl = paymentData.OrderUrl,
                    itemDesc = paymentData.Description,
                }),
                Receipt = new InvoiceDataBase.ReceiptOptions() { Enabled = true, }
            };

            var invoice = await client.CreateInvoice(settings.BtcPayStoreID, req);
            return invoice;

        }

        public async Task<string> CreateRefund(BtcPaySettings settings, RefundPaymentRequest refundRequest)
        {
            var client = GetClient(settings);
            var invoice = await client.GetInvoicePaymentMethods(settings.BtcPayStoreID,
                refundRequest.PaymentTransaction.AuthorizationTransactionId);
            var pm = (invoice.FirstOrDefault(p => p.Payments.Any()) ?? invoice.First()).PaymentMethod;
            var refundInvoiceRequest = new RefundInvoiceRequest() {
                Name = "Refund order " + refundRequest.PaymentTransaction.OrderGuid,
                PaymentMethod = pm,
            };
            if (refundRequest.IsPartialRefund)
            {
                refundInvoiceRequest.Description = "Partial refund";
                refundInvoiceRequest.RefundVariant = RefundVariant.Custom;
                refundInvoiceRequest.CustomAmount = (decimal) refundRequest.AmountToRefund;
                refundInvoiceRequest.CustomCurrency = refundRequest.PaymentTransaction.CurrencyCode;
            }
            else
            {
                refundInvoiceRequest.Description = "Full";
                refundInvoiceRequest.PaymentMethod = "BTC";
                refundInvoiceRequest.RefundVariant = RefundVariant.Fiat;
            }


            var refund = await client.RefundInvoice(settings.BtcPayStoreID,
                refundRequest.PaymentTransaction.AuthorizationTransactionId, refundInvoiceRequest);

            return refund.ViewLink;
        }

        public async Task<bool> UpdateOrderWithInvoice(BtcPaySettings settings, Order order, string invoiceId)
        {
            try
            {
                var invoice = await GetInvoice(settings, invoiceId);
                return await UpdateOrderWithInvoice(order, invoice, null);
            }
            catch (Exception e)
            {
                order.PaymentStatusId = PaymentStatus.Voided;
                await _orderService.InsertOrderNote(new OrderNote {
                    OrderId = order.Id,
                    Note = $"BTCPayServer: Error updating order status with invoice {invoiceId} - {e.Message}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                return true;
            }
        }

        public async Task<bool> UpdateOrderWithInvoice(Order order, InvoiceData invoiceData,  WebhookInvoiceEvent? webhookEvent)
        {
            if (order.PaymentMethodSystemName != "Payments.BTCPayServer")
                return false;

            var newPaymentStatus = order.PaymentStatusId;
            var newOrderStatus = order.OrderStatusId;

            var tagAuthorizationTransactionId = $"AuthorizationTransactionId#{invoiceData.Id}";
            if (!order.OrderTagExists(new OrderTag() { Name = tagAuthorizationTransactionId }))
                order.OrderTags.Add(tagAuthorizationTransactionId);

            switch (invoiceData.Status)
            {
                case InvoiceStatus.New:
                    newPaymentStatus = PaymentStatus.Pending;
                    break;
                case InvoiceStatus.Processing:
                    newPaymentStatus = PaymentStatus.Authorized;
                    newOrderStatus = (int)OrderStatusSystem.Pending;
                    break;
                case InvoiceStatus.Expired:
                    newOrderStatus = (int)OrderStatusSystem.Cancelled;
                    newPaymentStatus = PaymentStatus.Voided;
                    break;
                case InvoiceStatus.Invalid:
                    newPaymentStatus = PaymentStatus.Voided;
                    newOrderStatus = (int)OrderStatusSystem.Cancelled;

                    break;
                case InvoiceStatus.Settled:
                    newPaymentStatus = PaymentStatus.Paid;
                    newOrderStatus = (int)OrderStatusSystem.Processing;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var updated = false;
            if (newPaymentStatus != order.PaymentStatusId)
            {
                order.PaymentStatusId = newPaymentStatus;
                updated = true;
            }

            if (newOrderStatus != order.OrderStatusId && order.OrderStatusId != (int)OrderStatusSystem.Complete)
            {
                order.OrderStatusId = newOrderStatus;
                updated = true;
            }

            var additionalMessage = GetAdditionalMessageFromWebhook(webhookEvent);
            if (updated)
            {
                additionalMessage = string.IsNullOrEmpty(additionalMessage) ? "" : $" - {additionalMessage}";
                await _orderService.InsertOrderNote(new OrderNote {
                    OrderId = order.Id,
                    Note = $"BTCPayServer: Order status updated to {newOrderStatus} and payment status to {newPaymentStatus} by BTCPay with invoice {invoiceData.Id}{additionalMessage}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                var sCaptureTransactionResult = order.OrderTags.First(a => a.StartsWith("CaptureTransactionResult#")).Split("#")[1];

                if (order.PaymentStatusId is PaymentStatus.Authorized or PaymentStatus.Paid &&
                    !string.IsNullOrEmpty(sCaptureTransactionResult)) {

                    var sAuthorizationTransactionResult = order.OrderTags.First(a => a.StartsWith("AuthorizationTransactionResult#")).Split("#")[1];
                    await _orderService.InsertOrderNote(new OrderNote {
                        OrderId = order.Id,
                        Note = $"BTCPayServer: Payment {(order.PaymentStatusId is PaymentStatus.Authorized ? $"received but waiting to confirm. <a href='{sAuthorizationTransactionResult}'>Click here for more information.</a>" : $"settled. <a href='{sCaptureTransactionResult}'>Click here for more information.</a>")}",
                        DisplayToCustomer = true,
                        CreatedOnUtc = DateTime.UtcNow
                    });
                }

                await _orderService.UpdateOrder(order);

                return true;
            }
            if (!string.IsNullOrEmpty(additionalMessage))
            {
                await _orderService.InsertOrderNote(new OrderNote {
                    OrderId = order.Id,
                    Note = $"BTCPayServer: {additionalMessage}",
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
            }

            return false;
        }

        public async Task<string> CreateWebHook(BtcPaySettings settings, string webHookUrl)
        {
            var client = GetClient(settings);
            var existing = await client.GetWebhooks(settings.BtcPayStoreID);
            var existingWebHook = existing.Where(x => x.Url == webHookUrl);
            foreach (var webhookData in existingWebHook)
            {
                await client.DeleteWebhook(settings.BtcPayStoreID, webhookData.Id);
            }

            var response = await client.CreateWebhook(settings.BtcPayStoreID,
                new CreateStoreWebhookRequest() {
                    Url = webHookUrl,
                    Enabled = true,
                    AuthorizedEvents = new StoreWebhookBaseData.AuthorizedEventsData() {
                        SpecificEvents = new[]
                        {
                            WebhookEventType.InvoiceReceivedPayment, WebhookEventType.InvoiceProcessing,
                            WebhookEventType.InvoiceExpired, WebhookEventType.InvoiceSettled,
                            WebhookEventType.InvoiceInvalid, WebhookEventType.InvoicePaymentSettled,
                        }
                    }
                });
            return response.Secret;
        }


        public async Task<InvoiceData> GetInvoice(BtcPaySettings settings, string invoiceId)
        {
            var client = GetClient(settings);
            return await client.GetInvoice(settings.BtcPayStoreID, invoiceId);
        }

        public BTCPayServerClient GetClient(BtcPaySettings settings)
        {
            return new BTCPayServerClient(new Uri(settings.BtcPayUrl), settings.ApiKey,
                _httpClientFactory.CreateClient("BTCPayServer"));
        }


        public async Task<string> GetStoreId(BtcPaySettings settings)
        {
            return (await GetClient(settings).GetStores()).First().Id;
        }

        private string? GetAdditionalMessageFromWebhook(WebhookInvoiceEvent? webhookEvent)
        {
            switch (webhookEvent?.Type)
            {
                case WebhookEventType.InvoiceReceivedPayment
                    when webhookEvent.ReadAs<WebhookInvoiceReceivedPaymentEvent>() is { } receivedPaymentEvent:
                    return
                        $"Payment detected ({receivedPaymentEvent.PaymentMethod}: {receivedPaymentEvent.Payment.Value})";
                case WebhookEventType.InvoicePaymentSettled
                    when webhookEvent.ReadAs<WebhookInvoicePaymentSettledEvent>() is { } receivedPaymentEvent:
                    return
                        $"Payment settled ({receivedPaymentEvent.PaymentMethod}: {receivedPaymentEvent.Payment.Value})";
                case WebhookEventType.InvoiceProcessing
                    when webhookEvent.ReadAs<WebhookInvoiceProcessingEvent>() is { } receivedPaymentEvent &&
                         receivedPaymentEvent.OverPaid:
                    return $"Invoice was overpaid.";
                case WebhookEventType.InvoiceExpired
                    when webhookEvent.ReadAs<WebhookInvoiceExpiredEvent>() is { } receivedPaymentEvent &&
                         receivedPaymentEvent.PartiallyPaid:
                    return $"Invoice expired but was paid partially.";
                default:
                    return null;
            }
        }

    }
}