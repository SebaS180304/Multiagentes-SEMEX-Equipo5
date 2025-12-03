using UnityEngine;

public class Waypoint : MonoBehaviour
{
    [Tooltip("ID del punto, por ejemplo E1, P2, S4...")]
    public string id = "P1";

    private void OnDrawGizmos()
    {
        // Esfera para ver el punto en la escena
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.25f);
    }
}
