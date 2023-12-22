using Grand.Business.Core.Interfaces.Checkout.Orders;
using Grand.Web.Common.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Payments.BTCPayServer.Controllers
{
    [Route("btcpayserver/order")]
    public class OrderBTCPayServerController : BaseController
    {
        private readonly IOrderService _orderService;

        public OrderBTCPayServerController(IOrderService orderService)
        {
            _orderService = orderService;
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> Index(Guid id)
        {
            var order = await _orderService.GetOrderByGuid(id);
            if (order is null)
            {
                return NotFound();
            }

            return RedirectToAction("Details", "Order", new { id = order.Id });
        }
    
    }
}
