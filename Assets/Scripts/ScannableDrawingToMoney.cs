using UnityEngine;

namespace ArtSimulator
{
    [RequireComponent(typeof(Collider))]
    public class ScannableDrawingToMoney : MonoBehaviour
    {
        public int dineroGanado = 100;
        private PlayerEconomy playerEconomy;

        [SerializeField] GameObject producto;

        private void Start()
        {
            playerEconomy = FindAnyObjectByType<PlayerEconomy>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("ZonaDinero") && gameObject.name == "Dibujo_Copia_Ilustración Artística")
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

            Destroy(producto != null ? producto : gameObject);
        }
    }
}
