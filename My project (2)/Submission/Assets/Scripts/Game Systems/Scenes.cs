using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;

public class Scenes : MonoBehaviour

{
    public void GoHome()
    {
        SceneManager.LoadScene(1);
    }
    public void FromHome()
    {
        SceneManager.LoadScene(2);
    }
   
    public void Normal()
    {
        SceneManager.LoadScene(3);
    }

    public void Hungry()
    {
        SceneManager.LoadScene(4);
    }

    public void Starvation()
    {
        SceneManager.LoadScene(5);
    }

    public void Tort()
    {
        SceneManager.LoadScene(6);
    }

    public void Quit()
    {
        Application.Quit();
    }





}
