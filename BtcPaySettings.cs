using Grand.Domain.Configuration;

namespace Payments.BTCPayServer
{
    /// <summary>
    /// Represents settings of BtcPay payment plugin
    /// </summary>
    public class BtcPaySettings : ISettings
    {
        /// <summary>
        /// The url of your BTCPay instance
        /// </summary>
        public string BtcPayUrl { get; set; }

        /// <summary>
        /// The API Key value generated in your BTCPay instance
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// The BTCPay StoreID
        /// </summary>
        public string? BtcPayStoreID { get; set; }

        /// <summary>
        /// The WebHook Secret value generated in your BTCPay instance
        /// </summary>
        public string? WebHookSecret { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }
        /// <summary>
        /// Additional fee
        /// </summary>
        public double AdditionalFee { get; set; }

        public int DisplayOrder { get; set; }

    }
}