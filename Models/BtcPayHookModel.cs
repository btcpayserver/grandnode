

namespace Payments.BTCPayServer.Models
{
    public class BtcPayHookModel
    {
        public bool enabled = true;
        public bool automaticRedelivery = true;
        public string? url;
        public BtcPayHookAuthorizedEvents authorizedEvents = new();
        public string? secret;        
    }

    public struct BtcPayHookAuthorizedEvents
    {
        public bool everything = true;

        public BtcPayHookAuthorizedEvents()
        {
        }
    }
}
