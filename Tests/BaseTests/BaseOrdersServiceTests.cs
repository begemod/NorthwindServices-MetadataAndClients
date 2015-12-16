﻿namespace Tests.BaseTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.ServiceModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using WCFServices.Cotracts;
    using WCFServices.DataContracts;

    internal interface IOrdersServiceChannel : IClientChannel, IOrdersService
    {
    }

    internal interface IOrdersSubscriptionChannel : IClientChannel, IOrdersSubscriptionService
    {
    }

    public class BaseOrdersServiceTests
    {
        private static readonly Dictionary<string, ChannelFactory<IOrdersServiceChannel>> ChannelFactories = new Dictionary<string, ChannelFactory<IOrdersServiceChannel>>();

        protected static void CloseChannelFactories()
        {
            foreach (var cf in ChannelFactories)
            {
                var channelFactory = cf.Value;

                if (channelFactory.State == CommunicationState.Faulted)
                {
                    channelFactory.Abort();
                }

                channelFactory.Close();
            }
        }

        protected void GetAllTest(string endpointConfigurationName)
        {
            var client = this.GetChannelFactory(endpointConfigurationName).CreateChannel();
            var allOrders = client.GetAll();

            Assert.IsTrue(allOrders != null && allOrders.Any());
        }

        protected void GetByIdTest(string endpointConfigurationName)
        {
            var client = this.GetChannelFactory(endpointConfigurationName).CreateChannel();

            var allOrders = client.GetAll();

            var orderId = allOrders.First().OrderId;

            var orderById = client.GetById(orderId);

            Assert.IsNotNull(orderById);
        }

        protected void CreateNewOrderFaultTest(string endpointConfigurationName)
        {
            using (var channel = this.GetChannelFactory(endpointConfigurationName))
            {
                var client = channel.CreateChannel();

                client.CreateNewOrder(null);
            }
        }

        protected void CreateNewOrderTest(string endpointConfigurationName)
        {
            using (var channel = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName))
            {
                var newOrder = this.CreateNewOrder(endpointConfigurationName);

                var client = channel.CreateChannel();
                var orderId = client.CreateNewOrder(newOrder);

                Assert.IsTrue(orderId > 0);

                var newOrderFromDB = client.GetById(orderId);

                Assert.IsNotNull(newOrderFromDB);
                Assert.IsTrue(newOrderFromDB.OrderState.Equals(OrderState.New));
            }
        }

        protected void UpdateOrderFaultOnNullParameterTest(string endpointConfigurationName)
        {
            using (var channel = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName))
            {
                var client = channel.CreateChannel();
                client.UpdateOrder(null);
            }
        }

        protected void UpdateOrderFaultOnAttemptNotInNewStateTest(string endpointConfigurationName)
        {
            using (var channel = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName))
            {
                var order = this.GetExistingOrder(endpointConfigurationName, dto => dto.OrderState.Equals(OrderState.InWork) || dto.OrderState.Equals(OrderState.Closed));

                var client = channel.CreateChannel();

                client.UpdateOrder(order);
            }
        }

        protected void DeleteOrderFaultOnAttemptToDeleteNotExistingOrderTest(string endpointConfigurationName)
        {
            using (var channel = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName))
            {
                var client = channel.CreateChannel();

                client.DeleteOrder(-1);
            }
        }

        protected void DeleteOrderFaultOnAttemptToDeleteClosedOrderTest(string endpointConfigurationName)
        {
            using (var channel = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName))
            {
                var order = this.GetExistingOrder(endpointConfigurationName, dto => dto.OrderState.Equals(OrderState.Closed));

                if (order != null)
                {
                    var client = channel.CreateChannel();

                    client.DeleteOrder(order.OrderId);
                }
            }
        }

        protected void DeleteOrderTest(string endpointConfigurationName)
        {
            using (var channel = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName))
            {
                var newOrder = this.CreateNewOrder(endpointConfigurationName);

                var client = channel.CreateChannel();

                var orderId = client.CreateNewOrder(newOrder);

                var affectedRows = client.DeleteOrder(orderId);

                Assert.AreEqual(affectedRows, 1);
            }
        }

        protected void ProcessOrderFaultTest(string endpointConfigurationName)
        {
            using (var channel = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName))
            {
                var order = this.GetExistingOrder(endpointConfigurationName, dto => dto.OrderState.Equals(OrderState.InWork) || dto.OrderState.Equals(OrderState.Closed));

                var client = channel.CreateChannel();

                client.ProcessOrder(order.OrderId);
            }
        }

        protected void ProcessOrderTest(string endpointConfigurationName)
        {
            using (var channel = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName))
            {
                var newOrder = this.CreateNewOrder(endpointConfigurationName);

                var client = channel.CreateChannel();

                var newOrderId = client.CreateNewOrder(newOrder);

                client.ProcessOrder(newOrderId);

                var newOrderFromDB = client.GetById(newOrderId);

                Assert.IsTrue(newOrderFromDB.OrderState.Equals(OrderState.InWork));
            }
        }

        protected void CloseOrderFaultTest(string endpointConfigurationName)
        {
            using (var channel = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName))
            {
                var order = this.GetExistingOrder(endpointConfigurationName, dto => dto.OrderState.Equals(OrderState.New) || dto.OrderState.Equals(OrderState.Closed));

                var client = channel.CreateChannel();

                client.CloseOrder(order.OrderId);
            }
        }

        protected void CloseOrderTest(string endpointConfigurationName)
        {
            using (var channel = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName))
            {
                var newOrder = this.CreateNewOrder(endpointConfigurationName);

                var client = channel.CreateChannel();

                var newOrderId = client.CreateNewOrder(newOrder);

                client.ProcessOrder(newOrderId);

                client.CloseOrder(newOrderId);

                var newOrderFromDB = client.GetById(newOrderId);

                Assert.IsTrue(newOrderFromDB.OrderState.Equals(OrderState.Closed));
            }
        }

        protected void SubscribeUnsubscribeOnOrderStatusChangingEventsTest(string endpointConfigurationName)
        {
            var callbacksClient = new SubscriptionServiceClient();
            using (var channel = new DuplexChannelFactory<IOrdersSubscriptionChannel>(callbacksClient, endpointConfigurationName))
            {
                var clientIdentifier = Guid.NewGuid().ToString();

                var client = channel.CreateChannel();

                var subscribeResult = client.Subscribe(clientIdentifier);

                Assert.IsTrue(subscribeResult);

                var unsubscribeResult = client.Unsubscribe(clientIdentifier);

                Assert.IsTrue(unsubscribeResult);
            }
        }

        protected void SimulateLongRunningOperationTest(string endpointConfigurationName)
        {
            using (var channel = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName))
            {
                const byte OperationRunningDurationInSeconds = 10;

                var client = channel.CreateChannel();

                var startAt = DateTime.Now;
                Console.WriteLine(DateTime.Now.ToLongTimeString());

                client.SimulateLongRunningOperation(OperationRunningDurationInSeconds);

                var endAt = DateTime.Now;
                Console.WriteLine(DateTime.Now.ToLongTimeString());

                var duration = endAt - startAt;

                Assert.IsTrue(duration.TotalSeconds >= OperationRunningDurationInSeconds);
            }
        }

        private ChannelFactory<IOrdersServiceChannel> GetChannelFactory(string endpointConfigurationName)
        {
            ChannelFactory<IOrdersServiceChannel> channelFactory;

            if (!ChannelFactories.ContainsKey(endpointConfigurationName))
            {
                channelFactory = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName);
                ChannelFactories.Add(endpointConfigurationName, channelFactory);
            }
            else
            {
                channelFactory = ChannelFactories[endpointConfigurationName];

                if (channelFactory.State == CommunicationState.Closing || channelFactory.State == CommunicationState.Closed)
                {
                    channelFactory = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName);
                    ChannelFactories[endpointConfigurationName] = channelFactory;
                }
            }

            return channelFactory;
        }

        private OrderDTO CreateNewOrder(string endpointConfigurationName)
        {
            var order = this.GetExistingOrder(endpointConfigurationName);
            order.OrderId = 0;
            order.RequiredDate = DateTime.Now.AddDays(1);

            return order;
        }

        private OrderDTO GetExistingOrder(string endpointConfigurationName, Func<OrderDTO, bool> predicate = null)
        {
            using (var channel = new ChannelFactory<IOrdersServiceChannel>(endpointConfigurationName))
            {
                var client = channel.CreateChannel();

                var allOrders = client.GetAll();

                predicate = predicate ?? (dto => true);

                return allOrders.First(predicate);
            }
        }
    }

    internal class SubscriptionServiceClient : IBroadcastCallback
    {
        public void OrderStatusIsChanged(int orderId)
        {
            Console.WriteLine("Status of order with Id = {0} has been changed", orderId);
        }
    }
}