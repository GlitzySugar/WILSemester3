using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (this.CompareTag("HomeDoor") && other.CompareTag("Player")) {SceneManager.LoadScene(2); }
        else { SceneManager.LoadScene(1); }
    }
   
}
