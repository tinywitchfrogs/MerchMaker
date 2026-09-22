using UnityEngine;

namespace ArtSimulator
{
    [RequireComponent(typeof(Collider))]
    public class ScannableDrawingToMoney : MonoBehaviour
    {
        public int dineroGanado = 100;
        private PlayerEconomy playerEconomy;

        private void Start()
        {
            playerEconomy = FindObjectOfType<PlayerEconomy>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("ZonaDinero") && gameObject.name == "ScannableDrawing")
            {
                ConvertirADinero();
            }
        }

        private void ConvertirADinero()
        {
            if (playerEconomy != null)
            {
                playerEconomy.AgregarDinero(dineroGanado);
            }

            Destroy(gameObject);
        }
    }
}
