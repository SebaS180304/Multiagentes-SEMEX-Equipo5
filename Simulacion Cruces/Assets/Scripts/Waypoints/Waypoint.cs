using UnityEngine;

public class Waypoint : MonoBehaviour
{
    [Tooltip("ID del punto, por ejemplo E1, P2, S4...")]
    public string id = "P1";

    [Header("Semáforo (opcional)")]
    [Tooltip("Marcar si este punto es donde se detiene el vehículo por el semáforo.")]
    public bool isStopPoint = false;

    [Tooltip("ID del semáforo que controla este punto (ej. TL1). Solo si es punto de alto.")]
    public string trafficLightId;

    private void OnDrawGizmos()
    {
        Gizmos.color = isStopPoint ? Color.red : Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.25f);
    }
}
