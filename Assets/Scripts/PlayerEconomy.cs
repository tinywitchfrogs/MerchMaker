using UnityEngine;

public class PlayerEconomy : MonoBehaviour
{
    public int dinero = 0;

    public void AgregarDinero(int cantidad)
    {
        dinero += cantidad;
        Debug.Log("Dinero actual: " + dinero);
    }
}
