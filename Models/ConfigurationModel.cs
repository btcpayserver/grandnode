using Grand.Infrastructure.ModelBinding;
using Grand.Infrastructure.Models;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Payments.BTCPayServer.Models
{
    public class ConfigurationModel : BaseModel
    {

        public string? StoreScope { get; set; }

        [GrandResourceDisplayName("Plugins.Payments.BTCPayServer.BtcPayUrl")]
        [Required]
        public string? BtcPayUrl { get; set; }

        [GrandResourceDisplayName("Plugins.Payments.BTCPayServer.ApiKey")]
        public string? ApiKey { get; set; }

        [GrandResourceDisplayName("Plugins.Payments.BTCPayServer.BtcPayStoreID")]
        public string? BtcPayStoreID { get; set; }

        [GrandResourceDisplayName("Plugins.Payments.BTCPayServer.WebHookSecret")]
        public string? WebHookSecret { get; set; }


        [GrandResourceDisplayName("Plugins.Payments.BTCPayServer.AdditionalFee")]
        public double AdditionalFee { get; set; }

        [GrandResourceDisplayName("Plugins.Payments.BTCPayServer.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }


        public bool IsConfigured()
        {
            return
                !string.IsNullOrEmpty(ApiKey) &&
                !string.IsNullOrEmpty(BtcPayStoreID) &&
                !string.IsNullOrEmpty(BtcPayUrl) &&
                !string.IsNullOrEmpty(WebHookSecret);
        }
    }


}