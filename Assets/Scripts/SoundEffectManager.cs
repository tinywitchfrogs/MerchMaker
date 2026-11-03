using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;

public class SoundEffectManager : MonoBehaviour
{
    private static SoundEffectManager Instance;

    private static AudioSource audioSource;
    private static SoundEffectLibrary soundEffectLibrary;
    //[SerializeField] private Slider sfxSlider;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            audioSource = GetComponent<AudioSource>();
            soundEffectLibrary = GetComponent<SoundEffectLibrary>();
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

public static void Play(string soundName)
{
    if (soundEffectLibrary == null || audioSource == null) return;
    AudioClip audioClip = soundEffectLibrary.GetRandomClip(soundName);
    if(audioClip != null)
    {
        audioSource.PlayOneShot(audioClip);
    }
}

//volume slider https://www.youtube.com/watch?v=rAX_r0yBwzQ

}
