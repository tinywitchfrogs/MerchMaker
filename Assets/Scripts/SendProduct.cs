using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class SendProduct : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Dibujo")) //&& Object.FindAnyObjectByType<Script>(ScannableDrawing.isOriginal == false))
        {
            Debug.Log($"<color=green>[TabletButtonTrigger]</color> ({gameObject.name}) Producto enviado");
            SoundEffectManager.Play("ka-ching");
            Destroy(other.gameObject);
        }
    }
}
