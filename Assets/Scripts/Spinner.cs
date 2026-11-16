using UnityEngine;

// Basit donus (yel degirmeni kanatlari vb.)
public class Spinner : MonoBehaviour
{
    public Vector3 axis = Vector3.forward;
    public float degPerSec = 30f;
    void Update() { transform.Rotate(axis, degPerSec * Time.deltaTime, Space.Self); }
}
