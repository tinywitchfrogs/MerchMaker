using System;
using UnityEngine;

public class PlayerEconomy : MonoBehaviour
{
    private static PlayerEconomy instance;
    public static PlayerEconomy Instance
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<PlayerEconomy>();
            return instance;
        }
        private set => instance = value;
    }

    public int dinero = 0;
    public event Action<int> OnMoneyChanged;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    public void AgregarDinero(int cantidad)
    {
        dinero += cantidad;
        Debug.Log("Dinero actual: " + dinero);
        OnMoneyChanged?.Invoke(dinero);
    }
}
