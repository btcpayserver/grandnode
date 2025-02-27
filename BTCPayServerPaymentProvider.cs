﻿using Grand.Business.Core.Enums.Checkout;
using Grand.Business.Core.Interfaces.Checkout.Orders;
using Grand.Business.Core.Interfaces.Checkout.Payments;
using Grand.Business.Core.Interfaces.Common.Directory;
using Grand.Business.Core.Interfaces.Common.Localization;
using Grand.Business.Core.Utilities.Checkout;
using Grand.Domain.Customers;
using Grand.Domain.Orders;
using Grand.Domain.Payments;
using Grand.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Payments.BTCPayServer.Models;
using Payments.BTCPayServer.Services;

#nullable enable

namespace Payments.BTCPayServer
{
    public class BTCPayServerPaymentProvider : IPaymentProvider
    {
        private readonly ITranslationService _translationService;
        private readonly BtcPaySettings _btcPaySettings;

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWorkContext _workContext;
        private readonly ILogger <BTCPayServerPaymentProvider> _logger;
        private readonly IOrderService _orderService;
        private readonly LinkGenerator _linkGenerator;
        private readonly Func<BtcPayService> _btcPayService;


        public BTCPayServerPaymentProvider(
            IHttpContextAccessor httpContextAccessor,
            ITranslationService translationService,
            IServiceProvider serviceProvider,
            IWorkContext workContext,
            BtcPaySettings btcPaySettings,
            LinkGenerator linkGenerator,
            IOrderService orderService,
            ILogger<BTCPayServerPaymentProvider> logger,
            Func<BtcPayService> btcPayService)
        {
            _httpContextAccessor = httpContextAccessor;
            _translationService = translationService;
            _serviceProvider = serviceProvider;
            _workContext = workContext;
            _btcPaySettings = btcPaySettings;
            _linkGenerator = linkGenerator;
            _orderService = orderService;
            _logger = logger;
            _btcPayService = btcPayService;
        }

        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;


        public string LogoURL => "/Plugins/Payments.BTCPayServer/logo.jpg";

        public string ConfigurationUrl => "/Admin/BTCPayServer/Configure";

        public string SystemName => "Payments.BTCPayServer";

        public string FriendlyName => "BTCPay - Pay in bitcoins";

        public int Priority => _btcPaySettings.DisplayOrder;

        public IList<string> LimitedToStores => new List<string>();

        public IList<string> LimitedToGroups => new List<string>();


        Task IPaymentProvider.CancelPayment(PaymentTransaction paymentTransaction)
        {
            return Task.CompletedTask;
        }


        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <returns>Result</returns>
        public async Task<bool> CanRePostRedirectPayment(PaymentTransaction paymentTransaction)
        {
            if (paymentTransaction == null)
                throw new ArgumentNullException(nameof(paymentTransaction));

            if (paymentTransaction.TransactionStatus == TransactionStatus.Pending)
                return await Task.FromResult(true);

            return await Task.FromResult(false);

        }

        public async Task<bool> SupportCapture()
        {
            return await Task.FromResult(false);
        }

        public async Task<CapturePaymentResult> Capture(PaymentTransaction paymentTransaction)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return await Task.FromResult(result);
        }

        public async Task<string> Description()
        {
            return await Task.FromResult(_translationService.GetResource("Plugins.Payments.BTCPayServer.PaymentMethodDescription2"));
        }

        public async Task<double> GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            if (_btcPaySettings.AdditionalFee <= 0)
                return _btcPaySettings.AdditionalFee;

            double result;
            if (_btcPaySettings.AdditionalFeePercentage)
            {
                //percentage
                var orderTotalCalculationService = _serviceProvider.GetRequiredService<IOrderCalculationService>();
                var subtotal = await orderTotalCalculationService.GetShoppingCartSubTotal(cart, true);
                result = (float)subtotal.subTotalWithDiscount * (float)_btcPaySettings.AdditionalFee / 100f;
            }
            else
            {
                result = _btcPaySettings.AdditionalFee;
            }

            if (!(result > 0)) return result;

            var currencyService = _serviceProvider.GetRequiredService<ICurrencyService>();            
            result = await currencyService.ConvertFromPrimaryStoreCurrency(result, _workContext.WorkingCurrency);

            return result;
        }

        public Task<string> GetControllerRouteName()
        {
            return Task.FromResult("Plugin.PaymentBTCPayServer");
        }


        public async Task<bool> HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return await Task.FromResult(false);
        }

        public Task<PaymentTransaction> InitPaymentTransaction()
        {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            return Task.FromResult<PaymentTransaction>(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        public async Task<ProcessPaymentResult> ProcessPayment(PaymentTransaction paymentTransaction)
        {
            var result = new ProcessPaymentResult();
            return await Task.FromResult(result);
        }

        public Task PostProcessPayment(PaymentTransaction paymentTransaction)
        {
            //nothing
            return Task.CompletedTask;
        }

        public async Task<string> PostRedirectPayment(PaymentTransaction paymentTransaction)
        {
            // implement process payment
            Order? order = null;
            var myStore = _workContext.CurrentStore;
            try
            {

                var lang = _workContext.WorkingLanguage;
                var langCode = (lang == null) ? "en" : lang.UniqueSeoCode;

                Customer? myCustomer = _workContext.CurrentCustomer;

                var invoice = await _btcPayService().CreateInvoice(_btcPaySettings, new PaymentDataModel() {
                    CurrencyCode = paymentTransaction.CurrencyCode,
                    Amount = (decimal)paymentTransaction.TransactionAmount,
                    BuyerEmail = myCustomer.Email ?? (myCustomer.BillingAddress.Email ?? myCustomer.ShippingAddress.Email),
                    BuyerName = myCustomer.GetFullName(),
                    OrderID = paymentTransaction.OrderGuid.ToString(),
                    StoreID = myStore.Id,
                    CustomerID = myCustomer.Id,
                    Description = "From " + myStore.Name,
                    RedirectionURL = myStore.Url + "checkout/completed",
                    Lang = langCode,
                    OrderUrl = new Uri(new Uri(myStore.Url),
                            _linkGenerator.GetPathByAction("Index", "OrderBTCPayServer",
                                new { id = paymentTransaction.OrderGuid })).ToString()
                });

                order = await _orderService.GetOrderByGuid(paymentTransaction.OrderGuid);
                order.OrderTags.Add($"AuthorizationTransactionResult#{invoice.CheckoutLink}");
                order.OrderTags.Add($"AuthorizationTransactionId#{invoice.Id}");
                order.OrderTags.Add($"CaptureTransactionResult#{(invoice.Receipt?.Enabled is true ? invoice.CheckoutLink + "/receipt" : null)}");

                await _orderService.UpdateOrder(order);

                // _httpContextAccessor.HttpContext?.Response.Redirect(invoice.CheckoutLink);
                return invoice.CheckoutLink;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                if (order == null)
                {
                    order = await _orderService.GetOrderByGuid(paymentTransaction.OrderGuid);
                }
                await _orderService.InsertOrderNote(new OrderNote {
                    OrderId = order.Id,
                    Note = $"Error creating BTCPay payment.",
                    DisplayToCustomer = true,
                    CreatedOnUtc = DateTime.UtcNow
                });
                return string.Empty;
            }

        }

        public async Task<RefundPaymentResult> Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            try
            {
                var order = await _orderService.GetOrderByGuid(refundPaymentRequest.PaymentTransaction.OrderGuid);
                var sUrl = await _btcPayService().CreateRefund(_btcPaySettings, refundPaymentRequest);
                if (sUrl == null) { throw new Exception("Refund : Error with BTCPay"); }
                result.NewTransactionStatus = refundPaymentRequest.IsPartialRefund ? TransactionStatus.PartiallyRefunded : TransactionStatus.Refunded;
                await _orderService.InsertOrderNote(new OrderNote {
                    OrderId = order.Id,
                    Note = _translationService.GetResource("Plugins.Payments.BTCPayServer.NoteRefund") + $" <a href='{sUrl}'>{sUrl}</a>",
                    DisplayToCustomer = true,
                    CreatedOnUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                result.AddError(ex.Message);
            }

            return await Task.FromResult(result);
        }

        public async Task<PaymentTransaction> SavePaymentInfo(IDictionary<string, string> model)
        {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            return await Task.FromResult<PaymentTransaction>(null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        public async Task<bool> SkipPaymentInfo()
        {
            return await Task.FromResult(false);
        }


        public async Task<bool> SupportPartiallyRefund()
        {
            return await Task.FromResult(true);
        }

        public async Task<bool> SupportRefund()
        {
            return await Task.FromResult(true);
        }

        public async Task<bool> SupportVoid()
        {
            return await Task.FromResult(true);
        }

        public async Task<VoidPaymentResult> Void(PaymentTransaction paymentTransaction)
        {
            try
            {
                return new VoidPaymentResult() {
                    NewTransactionStatus = paymentTransaction.TransactionStatus
                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return new VoidPaymentResult() { NewTransactionStatus = TransactionStatus.Voided };
            }
        }

        public async Task<IList<string>> ValidatePaymentForm(IDictionary<string, string> model)
        {
            return await Task.FromResult(new List<string>());
        }

    }
}
