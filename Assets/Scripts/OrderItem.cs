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
        public string customerName;

        private static readonly string[] CustomerPool = new string[]
        {
            // Nombres de compradores reales y de tiendas online
            "Camila Soto", "Matías Rojas", "Valentina Paz", "Lucas Araya", "Sofía Herrera",
            "Diego Morales", "Fernanda Gómez", "Benja Torres", "Catalina Vega", "Ignacio Bravo",
            "Javiera Silva", "Tomás Castro", "Constanza M.", "Martín_C", "Paula Reyes",
            "Nicolás Díaz", "Daniela Muñoz", "Sebastián P.", "Andrea V.", "Gabriel_S",
            // Nicknames y usuarios creativos de comunidades de arte/internet
            "PixelCat99", "NekoLover", "AestheticFox", "ArtHunter_21", "SakuraMoon",
            "MochiSweet", "StarryVibes", "PandaChibi", "CoffeeBean_9", "KitsuneArt",
            "NeoWave88", "LunaDraws", "CyberOtaku", "DinoNugget", "CosmicBunny",
            "GamerChilensis", "ChibiMaster", "KawaiiFox", "RetroGamer_CL", "SweetBerry"
        };

        public OrderItem(ProductItem product, int unitsRequested, string customer = null)
        {
            this.orderId = "PED-" + UnityEngine.Random.Range(1000, 9999);
            this.product = product;
            this.unitsRequested = Mathf.Max(1, unitsRequested);
            this.unitsDelivered = 0;
            this.orderTimeFormatted = DateTime.Now.ToString("HH:mm");
            this.isCompleted = false;
            this.customerName = !string.IsNullOrEmpty(customer) 
                ? customer 
                : CustomerPool[UnityEngine.Random.Range(0, CustomerPool.Length)];
        }

        public string GetCustomerName()
        {
            if (string.IsNullOrEmpty(customerName))
            {
                customerName = CustomerPool[UnityEngine.Random.Range(0, CustomerPool.Length)];
            }
            return customerName;
        }
    }
}
