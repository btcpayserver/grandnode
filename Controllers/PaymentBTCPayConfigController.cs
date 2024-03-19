using BTCPayServer.Client;
using Grand.Web.Common.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payments.BTCPayServer.Models;

namespace Payments.BTCPayServer.Controllers
{
    public class PaymentBTCPayConfigController : BaseController
    {

        public PaymentBTCPayConfigController()
        {
        }


        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult GetAutomaticApiKeyConfig(string ssid, string btcpayuri)
        {
            Request.Form.TryGetValue("apiKey", out var apiKey);
            Request.Form.TryGetValue("permissions[]", out var permissions);

            Permission.TryParse(permissions.FirstOrDefault(), out var permission);

            var model = new BtcPayConfigModel(ssid, btcpayuri, permission.Scope, apiKey!);
            return View(model);
        }
    }
}
