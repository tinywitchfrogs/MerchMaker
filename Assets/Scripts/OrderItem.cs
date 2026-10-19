using System;
using UnityEngine;

namespace ArtSimulator
{
    [Serializable]
    public class OrderItem
    {
        public string orderId;
        public ProductItem product;
        public int unitsRequested;
        public int unitsDelivered;
        public string orderTimeFormatted;
        public bool isCompleted;

        public OrderItem(ProductItem product, int unitsRequested)
        {
            this.orderId = "PED-" + UnityEngine.Random.Range(1000, 9999);
            this.product = product;
            this.unitsRequested = Mathf.Max(1, unitsRequested);
            this.unitsDelivered = 0;
            this.orderTimeFormatted = DateTime.Now.ToString("HH:mm");
            this.isCompleted = false;
        }
    }
}
