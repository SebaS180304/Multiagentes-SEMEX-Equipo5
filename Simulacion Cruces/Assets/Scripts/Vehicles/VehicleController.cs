using UnityEngine;

public class VehicleController : MonoBehaviour
{
    [Header("Movimiento base")]
    private float baseSpeed = 1f;          // m/s
    private float baseRotationSpeed = 1f;  // giro

    private Transform[] route;
    private int currentIndex = 0;
    private bool initialized = false;

    public void SetRoute(Transform[] routePoints)
    {
        route = routePoints;
        currentIndex = 0;
        initialized = route != null && route.Length > 0;

        if (initialized)
        {
            // Colocar el coche en el primer punto
            transform.position = route[0].position;

            if (route.Length > 1)
            {
                Vector3 dir = (route[1].position - route[0].position).normalized;
                if (dir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }
    }

    private void Start()
    {
        // Registrar vehículo en el manager global (si existe)
        if (VehicleManager.Instance != null)
            VehicleManager.Instance.RegisterVehicle(this);
    }

    private void OnDestroy()
    {
        // Desregistrar
        if (VehicleManager.Instance != null)
            VehicleManager.Instance.UnregisterVehicle(this);
    }

    private void Update()
    {
        if (!initialized || route == null || route.Length == 0)
            return;

        // Obtener multiplicadores globales (por si el manager está ausente en pruebas)
        float speedMultiplier = 1f;
        float rotationMultiplier = 1f;

        if (VehicleManager.Instance != null)
        {
            speedMultiplier = VehicleManager.Instance.globalSpeedMultiplier;
            rotationMultiplier = VehicleManager.Instance.globalRotationMultiplier;
        }

        float speed = baseSpeed * speedMultiplier;
        float rotationSpeed = baseRotationSpeed * rotationMultiplier;

        // Si ya llegamos al final de la ruta
        if (currentIndex >= route.Length)
        {
            Destroy(gameObject);
            return;
        }

        Transform target = route[currentIndex];
        Vector3 toTarget = target.position - transform.position;
        float distanceThisFrame = speed * Time.deltaTime;

        if (toTarget.magnitude <= distanceThisFrame)
        {
            // Llegamos al punto
            transform.position = target.position;
            currentIndex++;

            // Ajustar dirección hacia el siguiente
            if (currentIndex < route.Length)
            {
                Vector3 nextDir = (route[currentIndex].position - transform.position).normalized;
                if (nextDir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(nextDir, Vector3.up);
            }
        }
        else
        {
            // Avanzar hacia el punto
            Vector3 moveDir = toTarget.normalized;
            transform.position += moveDir * distanceThisFrame;

            // Girar suavemente hacia dirección de movimiento
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }
}
