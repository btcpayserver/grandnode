using BTCPayServer.Client.Models;
using Grand.Business.Core.Interfaces.Checkout.Orders;
using Grand.Business.Core.Interfaces.Common.Configuration;
using Grand.Web.Common.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
        private readonly Func<BtcPayService> _btcPayService;

        public PaymentBTCPayServerController(IOrderService orderService,
            ISettingService settingService,
            ILogger logger,
            Func<BtcPayService> btcPayService)
        {
            _settingService = settingService;
            _orderService = orderService;
            _logger = logger;
            _btcPayService = btcPayService;
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
                    _logger.LogError("Missing fields in request");
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);
                }

                var order = await _orderService.GetOrderByGuid(new Guid(orderId));
                if (order == null)
                {
                    _logger.LogError("Order not found");
                    return StatusCode(StatusCodes.Status422UnprocessableEntity);
                }


                var settings = await _settingService.LoadSetting<BtcPaySettings>(order.StoreId);

                if (settings.WebHookSecret is not null && !BtcPayService.CheckSecretKey(settings.WebHookSecret, jsonStr, BtcPaySecret))
                {
                    _logger.LogError("Bad secret key");
                    return StatusCode(StatusCodes.Status400BadRequest);
                }

                var invoice = await _btcPayService().GetInvoice(settings, webhookEvent.InvoiceId);
                await _btcPayService().UpdateOrderWithInvoice(order, invoice, webhookEvent);

                return StatusCode(StatusCodes.Status200OK);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(StatusCodes.Status400BadRequest);
            }
        }


        public IActionResult PaymentInfo()
        {
            return View();
        }

    }


}
