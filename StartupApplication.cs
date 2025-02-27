using Grand.Business.Core.Interfaces.Checkout.Payments;
using Grand.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payments.BTCPayServer.Services;

namespace Payments.BTCPayServer
{
    public class StartupApplication : IStartupApplication
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IPaymentProvider, BTCPayServerPaymentProvider>();
            services.AddScoped<BtcPayService>();
            services.AddScoped<Func<BtcPayService>>(serviceProvider =>
            {
                return () => serviceProvider.GetRequiredService<BtcPayService>();
            });
        }

        public int Priority => 10;
        public void Configure(WebApplication application, IWebHostEnvironment webHostEnvironment)
        {

        }
        public bool BeforeConfigure => false;
    }

}
