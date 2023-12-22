using BTCPayServer.Client.Models;
using Grand.Business.Core.Interfaces.Checkout.Orders;
using Grand.Business.Core.Interfaces.Common.Configuration;
using Grand.Business.Core.Interfaces.Common.Logging;
using Grand.Domain.Logging;
using Grand.Web.Common.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Payments.BTCPayServer;
using Payments.BTCPayServer.Services;

namespace BTCPayServer.Controllers
{
    public class PaymentBTCPayServerController : BasePaymentController
    {
        private readonly ISettingService _settingService;
        private readonly IOrderService _orderService;
        private readonly ILogger _logger;
        private readonly BtcPayService _btcPayService;

        public PaymentBTCPayServerController(IOrderService orderService,
            ISettingService settingService,
            ILogger logger,
            IHttpClientFactory httpClientFactory)
        {
            _settingService = settingService;
            _orderService = orderService;
            _logger = logger;
            _btcPayService = new BtcPayService(orderService, httpClientFactory);
        }


        [HttpPost]
        public async Task<IActionResult> Process([FromHeader(Name = "BTCPAY-SIG")] string BtcPaySig)
        {
            try
            {
                string jsonStr = await new StreamReader(Request.Body).ReadToEndAsync();
                var webhookEvent = JsonConvert.DeserializeObject<WebhookInvoiceEvent>(jsonStr);
                var BtcPaySecret = BtcPaySig.Split('=')[1];
                if (webhookEvent is null || webhookEvent?.InvoiceId?.StartsWith("__test__") is true || webhookEvent?.Type == WebhookEventType.InvoiceCreated)
                {
                    return Ok();
                }

                if (webhookEvent?.InvoiceId is null || webhookEvent.Metadata?.TryGetValue("orderId", out var orderIdToken) is not true || orderIdToken.ToString() is not { } orderId)
                {
                    await _logger.InsertLog(LogLevel.Error, "Missing fields in request");
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);
                }

                var order = await _orderService.GetOrderByGuid(new Guid(orderId));
                if (order == null)
                {
                    await _logger.InsertLog(LogLevel.Error, "Order not found");
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);
                }


                var settings = _settingService.LoadSetting<BtcPaySettings>(order.StoreId);

                if (settings.WebHookSecret is not null && !BtcPayService.CheckSecretKey(settings.WebHookSecret, jsonStr, BtcPaySecret))
                {
                    await _logger.InsertLog(LogLevel.Error, "Bad secret key");
                    return StatusCode(StatusCodes.Status400BadRequest);
                }

                var invoice = await _btcPayService.GetInvoice(settings, webhookEvent.InvoiceId);
                await _btcPayService.UpdateOrderWithInvoice(order, invoice, webhookEvent);

                return StatusCode(StatusCodes.Status200OK);

            }
            catch (Exception ex)
            {
                await _logger.InsertLog(LogLevel.Error, ex.Message);
                return StatusCode(StatusCodes.Status400BadRequest);
            }
        }


        public IActionResult PaymentInfo()
        {
            return View();
        }

    }


}
