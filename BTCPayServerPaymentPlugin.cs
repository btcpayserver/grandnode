﻿using Grand.Business.Common.Services.Localization;
using Grand.Business.Core.Extensions;
using Grand.Business.Core.Interfaces.Common.Configuration;
using Grand.Business.Core.Interfaces.Common.Directory;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Domain.Directory;
using Grand.Infrastructure.Plugins;

namespace Payments.BTCPayServer
{
    public class BTCPayServerPaymentPlugin( ICurrencyService currencyService,
                                            ISettingService settingService,
                                            IPluginTranslateResource pluginTranslateResource) : BasePlugin, IPlugin
    {
        public override string ConfigurationUrl()
        {
            return "/Admin/BTCPayServer/Configure";
        }


        private async Task AddBTCCurrency()
        {
            try
            {
                await currencyService.InsertCurrency(new Currency {
                    DisplayLocale = "en-US",
                    Name = "Bitcoin",
                    CurrencyCode = "BTC",
                    CustomFormatting = "{0} ₿",
                    Published = true,

                    DisplayOrder = 1,
                });

            }
            catch
            {
                // ignored
            }
        }

        public async override Task Install()
        {
            await settingService.SaveSetting(new BtcPaySettings {
                BtcPayUrl = "",
                ApiKey = "",
                BtcPayStoreID = "",
                WebHookSecret = ""
            });
            await AddBTCCurrency();

            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.AdditionalFee", "Additional fee");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.AdditionalFee.Hint", "The additional fee.");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.AdditionalFeePercentage", "Additional fee. Use percentage");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");

            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.Instructions", "<div class=\"mb-1\"><b>BTCPay Plugin for GrandNode</b></div>" +
                        "<div class=\"mb-1\">The plugin configuration can be done automatically or manually.</div>" +
                        "<div class=\"mb-1\"><br/><b>Automatic Configuration:</b></div>" +
                        "<ul>" +
                        "    <li>Enter the \"BTCPay Url\" parameter.</li>" +
                        "    <li>Click on the \"Configure automatically\" button to be redirected to the key creation page on your BTCPay server.</li>" +
                        "    <li>The \"API Key\", \"BTCPay Store ID\" and \"WebHook Secret\" parameters will be automatically filled. Save.</li>" +
                        "</ul>" +
                        "<div class=\"mb-1\"><br/><b>Manual Configuration:</b></div>" +
                        "<ul>" +
                        "    <li>The \"BTCPay Url,\" \"API Key,\" \"BTCPay Store ID,\" and \"WebHook Secret\" fields must be filled out.</li>" +
                        "    <li>To create the BTCPay API key, <a href =\"https://docs.btcpayserver.org/VirtueMart/#22-create-an-api-key-and-configure-permissions\" target=\"_blank\">read this</a>.<br/>" +
                        "        <i>Note: If you want to use the Refund feature, you must also add the \"Modify your stores\" permission.<br/>" +
                        "        After a refund, an order note is created, indicating the BTCPay link where the customer can request a refund." +
                        "        </i>" +
                        "    </li>" +
                        "    <li>To create the BTCPay WebHook, <a href =\"https://docs.btcpayserver.org/VirtueMart/#23-create-a-webhook-on-btcpay-server\" target=\"_blank\">read this</a>. (use the default secret code generated by BTCPay)</li>" +
                        "</ul>");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.PaymentMethodDescription", "Pay your order in bitcoins");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.PaymentMethodDescription2", "After completing the order you will be redirected to the merchant BTCPay instance, where you can make the Bitcoin payment for your order.");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.PaymentError", "Error processing the payment. Please try again and contact us if the problem persists.");

            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.BtcPayUrl", "BTCPay Url");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.BtcPayUrl.Hint", "The url of your BTCPay instance");

            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.CreateApiKey", "Create API key automatically");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.ApiKey", "API Key");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.ApiKey.Hint", "The API Key value generated in your BTCPay instance");

            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.BtcPayStoreID", "BTCPay Store ID");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.BtcPayStoreID.Hint", "The BTCPay Store ID");

            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.CreateWebhook", "Create webhook automatically");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.WebHookUrl", "WebHook Url");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.WebHookSecret", "WebHook Secret");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.WebHookSecret.Hint", "The WebHook Secret value generated in your BTCPay instance");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.WebHookInfo", "Here is the URL to set for the WebHook creation in BTCPay : ");
            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.WebHookNote", "Note: when testing the webhook from BTCPay, you should get an HTTP 422 error.<br/>" +
                "This is because BTCPay sends empty data while the GrandNode plugin expects real data.<br/>" +
                "This error therefore indicates that the webhook is indeed accessible from BTCPay.<br/>" +
                "With a real transaction, you can therefore expect correct operation.");

            await pluginTranslateResource.AddOrUpdatePluginTranslateResource("Plugins.Payments.BTCPayServer.NoteRefund", "Please visit the following link to claim your refund: ");
            await base.Install();
        }

        public async override Task Uninstall()
        {
            //settings
            await settingService.DeleteSetting<BtcPaySettings>();

            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.AdditionalFee");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.AdditionalFee.Hint");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.AdditionalFeePercentage");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.AdditionalFeePercentage.Hint");

            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.Instructions");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.PaymentMethodDescription");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.PaymentMethodDescription2");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.PaymentError");

            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.BtcPayUrl");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.BtcPayUrl.Hint");

            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.CreateApiKey");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.ApiKey");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.ApiKey.Hint");

            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.BtcPayStoreID");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.BtcPayStoreID.Hint");

            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.CreateWebhook");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.WebHookUrl");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.WebHookSecret");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.WebHookSecret.Hint");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.WebHookInfo");
            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.WebHookNote");

            await pluginTranslateResource.DeletePluginTranslationResource("Plugins.Payments.BTCPayServer.NoteRefund");
            await base.Uninstall();
        }
    }
}
