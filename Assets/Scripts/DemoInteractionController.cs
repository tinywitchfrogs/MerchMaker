using UnityEngine;
using UnityEngine.UI;

namespace ArtSimulator
{
    public class DemoInteractionController : MonoBehaviour
    {
        private void Awake()
        {
            // Eliminar cualquier HUD remanente de guías obsoletas en la esquina inferior izquierda
            GameObject oldHud = GameObject.Find("DemoScreenHUD");
            if (oldHud != null)
            {
                Destroy(oldHud);
            }
        }
    }
}
