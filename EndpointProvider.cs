using Grand.Infrastructure.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Payments.BTCPayServer
{
    public class EndpointProvider : IEndpointProvider
    {
        public void RegisterEndpoint(IEndpointRouteBuilder endpointRouteBuilder)
        {
            endpointRouteBuilder.MapControllerRoute("Plugin.PaymentBTCPayServer",
                 "Plugins/PaymentBTCPayServer/PaymentInfo",
                 new { controller = "PaymentBTCPayServer", action = "PaymentInfo", area = "" }
            );            
        }
        public int Priority => 0;

    }
}
