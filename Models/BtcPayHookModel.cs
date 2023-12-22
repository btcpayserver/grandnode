using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payments.BTCPayServer.Models
{
    public class BtcPayHookModel
    {
        public bool enabled = true;
        public bool automaticRedelivery = true;
        public string url;
        public BtcPayHookAuthorizedEvents authorizedEvents = new BtcPayHookAuthorizedEvents();
        public string secret;

        public BtcPayHookModel()
        {
        }
    }

    public struct BtcPayHookAuthorizedEvents
    {
        public bool everything = true;

        public BtcPayHookAuthorizedEvents()
        {
        }
    }
}
