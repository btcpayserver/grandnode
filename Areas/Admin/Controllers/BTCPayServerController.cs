using BTCPayServer.Client;
using Grand.Business.Core.Interfaces.Common.Configuration;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Business.Core.Interfaces.Common.Security;
using Grand.Business.Core.Interfaces.Common.Stores;
using Grand.Domain.Common;
using Grand.Domain.Customers;
using Grand.Domain.Payments;
using Grand.Domain.Permissions;
using Grand.Infrastructure;
using Grand.Web.Common.Controllers;
using Grand.Web.Common.Filters;
using Grand.Web.Common.Security.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Payments.BTCPayServer.Models;
using Payments.BTCPayServer.Services;
using System.Web;

namespace Payments.BTCPayServer.Controllers
{
    [Area("Admin")]
    [AuthorizeAdmin]
    [PermissionAuthorize(PermissionSystemName.PaymentMethods)]
    public class BTCPayServerController : BasePaymentController
    {
        #region Fields

        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ITranslationService _translationService;
        private readonly IPermissionService _permissionService;
        private readonly LinkGenerator _linkGenerator;
        private readonly PaymentSettings _paymentSettings;
        private readonly Func<BtcPayService> _btcPayService;
        private readonly ILogger<BTCPayServerController> _logger;

        #endregion

        #region Ctor

        public BTCPayServerController(
            IWorkContext workContext,
            LinkGenerator linkGenerator,
            IStoreService storeService,
            ISettingService settingService,
            ITranslationService translationService,
            PaymentSettings settings,
            ILogger<BTCPayServerController> logger,
            IPermissionService permissionService,
            Func<BtcPayService> btcPayService)
        {
            _linkGenerator = linkGenerator;
            _workContext = workContext;
            _storeService = storeService;
            _settingService = settingService;
            _translationService = translationService;
            _permissionService = permissionService;
            _paymentSettings = settings;
            _logger = logger;
            _btcPayService = btcPayService;

        }

        #endregion

        #region Methods

        private async Task<string> GetActiveStore()
        {
            var stores = await _storeService.GetAllStores();
            if (stores.Count == 1)
                return stores.FirstOrDefault()!.Id;

            var storeId = _workContext.CurrentCustomer.GetUserFieldFromEntity<string>(SystemCustomerFieldNames.AdminAreaStoreScopeConfiguration);
            var store = await _storeService.GetStoreById(storeId);

            return store != null ? store.Id : "";
        }

        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.Authorize(StandardPermission.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await GetActiveStore();
            var btcPaySettings = await _settingService.LoadSetting<BtcPaySettings>(storeScope);

            var model = new ConfigurationModel {
                BtcPayUrl = string.IsNullOrWhiteSpace(btcPaySettings.BtcPayUrl) ? "" : btcPaySettings.BtcPayUrl,
                ApiKey = string.IsNullOrWhiteSpace(btcPaySettings.ApiKey) ? "" : btcPaySettings.ApiKey,
                BtcPayStoreID = string.IsNullOrWhiteSpace(btcPaySettings.BtcPayStoreID) ? "" : btcPaySettings.BtcPayStoreID,
                WebHookSecret = string.IsNullOrWhiteSpace(btcPaySettings.WebHookSecret) ? "" : btcPaySettings.WebHookSecret,
                AdditionalFee = btcPaySettings.AdditionalFee,
                AdditionalFeePercentage = btcPaySettings.AdditionalFeePercentage,
                StoreScope = storeScope
            };
            var store = await _storeService.GetStoreById(storeScope);

            ViewBag.UrlWebHook = (store.SslEnabled ? store.SecureUrl : store.Url) + "PaymentBTCPayServer/Process";

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model, string? command = null)
        {
            if (!await _permissionService.Authorize(StandardPermission.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await GetActiveStore();
            var settings = await _settingService.LoadSetting<BtcPaySettings>(storeScope);


            if (command == "delete")
            {
                settings.ApiKey = "";
                settings.BtcPayStoreID = "";
                settings.WebHookSecret = "";
                settings.BtcPayUrl = "";
                await _settingService.SaveSetting<BtcPaySettings>(settings, storeScope);
                await _settingService.ClearCache();
                ModelState.Clear();

                _paymentSettings.ActivePaymentProviderSystemNames.Remove("Payments.BTCPayServer");

                Success("Settings cleared and payment method deactivated");
                return await Configure();
            }

            if (command == "activate" && model.IsConfigured())
            {
                _paymentSettings.ActivePaymentProviderSystemNames.Add("Payments.BTCPayServer");

                await _settingService.SaveSetting<PaymentSettings>(_paymentSettings, storeScope);
                await _settingService.ClearCache();

                Success("Payment method activated");
            }

            if (command == "getautomaticapikeyconfig")
            {
                settings.BtcPayUrl = model.BtcPayUrl;
                await _settingService.SaveSetting<BtcPaySettings>(settings, storeScope);
                await _settingService.ClearCache();

                string? result = GetRedirectUri(settings);
                if (result != null)
                {
                    return Redirect(result);
                }

                Error("Cannot generate automatic configuration URL. Please check your BTCPay URL.");
                return await Configure();
            }


            //save settings
            settings.BtcPayUrl = model.BtcPayUrl?.Trim();
            settings.ApiKey = model.ApiKey?.Trim();
            settings.BtcPayStoreID = model.BtcPayStoreID?.Trim();
            settings.WebHookSecret = model.WebHookSecret?.Trim();
            settings.AdditionalFee = model.AdditionalFee;
            settings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */

            await _settingService.SaveSetting(settings, storeScope);

            //now clear settings cache
            await _settingService.ClearCache();

            Success(_translationService.GetResource("Admin.Plugins.Saved"));

            return await Configure();
        }


        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GetAutomaticApiKeyConfig(BtcPayConfigModel model)
        {
            var myStore = _workContext.CurrentStore;

            if (model.Ssid != myStore.Id)
            {
                _logger.LogError("GetAutomaticApiKeyConfig(): NotFound");
                return NotFound();
            }

            var settings = await _settingService.LoadSetting<BtcPaySettings>(myStore.Id);

            try
            {
                settings.BtcPayUrl = model.Btcpayuri;
                settings.ApiKey = model.ApiKey;
                settings.BtcPayStoreID = model.Scope;

                try
                {
                    if (model.Scope is null)
                    {
                        settings.BtcPayStoreID = await _btcPayService().GetStoreId(settings);
                    }

                    if (string.IsNullOrEmpty(settings.WebHookSecret))
                    {
                        var webhookUrl = (myStore.SslEnabled ? myStore.SecureUrl : myStore.Url) + "PaymentBTCPayServer/Process";
                        settings.WebHookSecret = await _btcPayService().CreateWebHook(settings, webhookUrl.ToString());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("1- " + ex.Message);
                }

                _paymentSettings.ActivePaymentProviderSystemNames.Add("Payments.BTCPayServer");

                await _settingService.SaveSetting<PaymentSettings>(_paymentSettings);
                await _settingService.SaveSetting<BtcPaySettings>(settings, myStore.Id);
                await _settingService.ClearCache();

                Success("Settings automatically configured and payment method activated.");


            }
            catch (Exception ex)
            {
                _logger.LogError("2- " + ex.Message);

            }
            return RedirectToAction(nameof(Configure));

        }

        private string? GetRedirectUri(BtcPaySettings btcPaySettings)
        {
            if (string.IsNullOrEmpty(btcPaySettings?.BtcPayUrl) ||
                !Uri.TryCreate(btcPaySettings?.BtcPayUrl, UriKind.Absolute, out var btcpayUri))
            {
                return null;
            }

            var myStore = _workContext.CurrentStore;

            var adminUrl = new Uri(new Uri("https://" + Request.Host.Value),
                _linkGenerator.GetPathByAction(HttpContext, "GetAutomaticApiKeyConfig", "PaymentBTCPayConfig",
                    new { ssid = myStore.Id, btcpayuri = btcpayUri, area = "" }));
            var uri = BTCPayServerClient.GenerateAuthorizeUri(btcpayUri,
                new[]
                {
                    Policies.CanCreateInvoice, // create invoices for payment
                    Policies.CanViewInvoices, // fetch created invoices to check status
                    Policies.CanModifyInvoices, // able to mark an invoice invalid in case merchant wants to void the order
                    Policies.CanModifyWebhooks, // able to create the webhook required automatically
                    Policies.CanViewStoreSettings, // able to fetch rates
                    Policies.CanCreateNonApprovedPullPayments // able to create refunds
                },
                true, true, ($"GrandNode{myStore.Id}", adminUrl));
            return uri + $"&applicationName={HttpUtility.UrlEncode(myStore.Name)}";
        }

        #endregion


    }
}