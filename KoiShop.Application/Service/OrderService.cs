﻿using AutoMapper;
using KoiShop.Application.Interfaces;
using KoiShop.Domain.Entities;
using KoiShop.Domain.Respositories;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static KoiShop.Application.Users.UserContext;
using KoiShop.Application.Dtos;
using Microsoft.IdentityModel.Tokens;
using PhoneNumbers;
using static Google.Rpc.Context.AttributeContext.Types;
using KoiShop.Application.Dtos.VnPayDtos;

namespace KoiShop.Application.Service
{
    public class OrderService : IOrderService
    {
        private readonly IMapper _mapper;
        private readonly IOrderRepository _orderRepository;
        private readonly IUserStore<User> _userStore;
        private readonly IUserContext _userContext;
        private readonly IVnPayService _vpnPayService;

        public OrderService(IMapper mapper, IOrderRepository orderRepository, IUserContext userContext, IUserStore<User> userStore, IVnPayService vpnPayService)
        {
            _mapper = mapper;
            _orderRepository = orderRepository;
            _userContext = userContext;
            _userStore = userStore;
            _vpnPayService = vpnPayService;
        }
        public async Task<IEnumerable<OrderDetailDtos>> GetOrderDetail()
        {
            if (_userContext.GetCurrentUser() == null || _userStore == null)
            {
                throw new ArgumentException("User context or user store is not valid.");
            }
            var userId = _userContext.GetCurrentUser().Id;
            if (userId == null)
            {
                return Enumerable.Empty<OrderDetailDtos>();
            }
            var od = await _orderRepository.GetOrderDetail();
            var oddto = _mapper.Map<IEnumerable<OrderDetailDtos>>(od);
            return oddto;
        }
        // abc
        public async Task<IEnumerable<OrderDtos>> GetOrder()
        {
            if (_userContext.GetCurrentUser() == null || _userStore == null)
            {
                throw new ArgumentException("User context or user store is not valid.");
            }
            var userId = _userContext.GetCurrentUser().Id;
            if (userId == null)
            {
                return Enumerable.Empty<OrderDtos>();
            }
            var order = await _orderRepository.GetOrder();
            var orderDto = _mapper.Map<IEnumerable<OrderDtos>>(order);
            return orderDto;
        }
        public async Task<IEnumerable<OrderDetailDtos>> GetOrderDetailById(int? id)
        {
            if (_userContext.GetCurrentUser() == null || _userStore == null)
            {
                throw new ArgumentException("User context or user store is not valid.");
            }
            var userId = _userContext.GetCurrentUser().Id;
            if (userId == null || id == null)
            {
                return Enumerable.Empty<OrderDetailDtos>();
            }
            var orderDetail = await _orderRepository.GetOrderDetailById((int)id);
            var orderDetailDto = _mapper.Map<IEnumerable<OrderDetailDtos>>(orderDetail);
            return orderDetailDto;
        }
        public async Task<IEnumerable<KoiDto>> GetKoiFromOrderDetail(int? orderId)
        {
            if (orderId == null)
            {
                return null;
            }
            var koi = await _orderRepository.GetKoiOrBatch<Koi>((int)orderId);
            var koiDto = _mapper.Map<IEnumerable<KoiDto>>(koi);
            return koiDto;
        }
        public async Task<IEnumerable<BatchKoiDto>> GetBatchFromOrderDetail(int? orderId)
        {
            if (orderId == null)
            {
                return null;
            }
            var batch = await _orderRepository.GetKoiOrBatch<BatchKoi>((int)orderId);
            var batchDto = _mapper.Map<IEnumerable<BatchKoiDto>>(batch);
            return batchDto;
        }

        public async Task<OrderEnum> AddOrders(List<CartDtoV2> carts, string method, int? discountId, string? phoneNumber, string? address)
        {
            if (_userContext.GetCurrentUser() == null || _userStore == null)
            {
                return OrderEnum.NotLoggedInYet;
            }
            var userId = _userContext.GetCurrentUser().Id;
            if (userId == null)
            {
                return OrderEnum.UserNotAuthenticated;
            }
            foreach (var cart in carts)
            {
                if ((cart.KoiId == null && cart.BatchKoiId == null) || (cart.KoiId.HasValue && cart.KoiId <= 0) || (cart.BatchKoiId.HasValue && cart.BatchKoiId <= 0) ||
                    (cart.KoiId.HasValue && cart.BatchKoiId.HasValue))
                {
                    return OrderEnum.InvalidParameters;
                }
            }
            if (phoneNumber == null || address.IsNullOrEmpty() || !IsPhoneNumberValid(phoneNumber, "VN"))
            {
                return OrderEnum.InvalidTypeParameters;
            }
            var cartItems = _mapper.Map<List<CartItem>>(carts);
            var order = await _orderRepository.AddToOrder(cartItems, discountId, phoneNumber, address);
            if (order)
            {
                var orderDetail = await _orderRepository.AddToOrderDetailFromCart(cartItems);
                if (orderDetail)
                {
                    var cartStatus = await _orderRepository.UpdateCartAfterBuy(cartItems);
                    if (cartStatus)
                    {
                        var koiandBatchStatus = await _orderRepository.UpdateKoiAndBatchStatus(cartItems);
                        if (koiandBatchStatus)
                        {
                            var payment = await _orderRepository.AddPayment("offline");
                            if (payment)
                            {
                                return OrderEnum.Success;
                            }
                            else
                            {
                                return OrderEnum.FailAddPayment;
                            }
                        }
                        else
                        {
                            return OrderEnum.FailUpdateFish;
                        }
                    }
                    else
                    {
                        return OrderEnum.FailUpdateCart;
                    }
                }
                else
                {
                    return OrderEnum.FailAdd;
                }
            }
            else
                return OrderEnum.Fail;
        }

        public async Task<PaymentDto> PayByVnpay(VnPaymentResponseFromFe request)
        {
            if (_userContext.GetCurrentUser() == null || _userStore == null)
            {
                return new PaymentDto
                {
                    Status = OrderEnum.NotLoggedInYet,
                    Message = "User not logged in."
                };
            }
            var userId = _userContext.GetCurrentUser().Id;
            if (userId == null)
            {
                return new PaymentDto
                {
                    Status = OrderEnum.UserNotAuthenticated,
                    Message = "User not authenticated."
                };
            }
            if (request == null)
            {
                return new PaymentDto
                {
                    Status = OrderEnum.InvalidParameters,
                    Message = "Paramaters can not identify"
                };
            }
            var response = _vpnPayService.ExecutePayment(request);
            if (response == null || !response.Success)
            {
                return new PaymentDto
                {
                    Status = OrderEnum.FailAddPayment,
                    Message = $"PaymentFail {response?.VnPayResponseCode}"
                };

            }
            var paymentUpdate = await _orderRepository.UpdatePayment();
            if (paymentUpdate)
            {
                return new PaymentDto
                {
                    Status = OrderEnum.Success,
                    Message = "Payment successful."
                };
            }
            else
            {
                return new PaymentDto
                {
                    Status = OrderEnum.Fail,
                    Message = "Payment unsuccessful."
                };
            }
        }
        private bool IsPhoneNumberValid(string phoneNumber, string regionCode)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return false;
            }

            try
            {
                var phoneNumberUtil = PhoneNumberUtil.GetInstance();
                var parsedNumber = phoneNumberUtil.Parse(phoneNumber, regionCode);
                return phoneNumberUtil.IsValidNumber(parsedNumber);
            }
            catch (NumberParseException)
            {
                return false;
            }
        }

    }
}
