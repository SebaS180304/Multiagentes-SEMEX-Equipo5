using UnityEngine;

public class VehicleSpawner : MonoBehaviour
{
    [Header("Entrada que representa este spawner")]
    public string entryId = "E1";   // E1, E2, E3, E4, E5

    [Header("Prefabs de vehículos")]
    public GameObject[] vehiclePrefabs;

    [Header("Espaciado mínimo en el punto de spawn")]
    [Tooltip("Distancia mínima al coche más cercano para poder hacer spawn")]
    public float minSpawnDistance = 2.0f;

    public GameObject SpawnVehicle(Transform[] routePoints)
    {
        // Validaciones de la ruta
        if (routePoints == null || routePoints.Length == 0 || routePoints[0] == null)
        {
            Debug.LogError($"VehicleSpawner ({entryId}): ruta inválida.");
            return null;
        }

        if (vehiclePrefabs == null || vehiclePrefabs.Length == 0)
        {
            Debug.LogError($"VehicleSpawner ({entryId}): no hay prefabs asignados.");
            return null;
        }

        // Posición del primer punto de la ruta
        Vector3 spawnPos = routePoints[0].position;

        // checar si hay espacio suficiente antes de spawnear
        if (!CanSpawnHere(spawnPos))
        {
            return null;
        }

        // Elegir prefab al azar
        GameObject prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Length)];

        // Rotación inicial mirando hacia el segundo punto de la ruta (si existe)
        Quaternion spawnRot = Quaternion.identity;

        if (routePoints.Length > 1 && routePoints[1] != null)
        {
            Vector3 dir = (routePoints[1].position - spawnPos).normalized;
            if (dir.sqrMagnitude > 0.0001f)
                spawnRot = Quaternion.LookRotation(dir, Vector3.up);
        }

        // Instanciar vehículo
        GameObject vehicleObj = Object.Instantiate(prefab, spawnPos, spawnRot);

        // Asegurarnos de que tiene VehicleController y asignar ruta
        VehicleController controller = vehicleObj.GetComponent<VehicleController>();
        if (controller == null)
        {
            Debug.LogError($"VehicleSpawner ({entryId}): el prefab {prefab.name} no tiene VehicleController.");
            return vehicleObj;
        }

        controller.SetRoute(routePoints);
        return vehicleObj;
    }

    /// <summary>
    /// Revisa si el punto de spawn está lo suficientemente libre
    /// (no hay otro vehículo demasiado cerca).
    /// </summary>
    private bool CanSpawnHere(Vector3 position)
    {
        // Buscamos TODOS los vehículos actuales en la escena
        VehicleController[] vehicles = UnityEngine.Object.FindObjectsByType<VehicleController>(
            FindObjectsSortMode.None
        );

        float minDistSq = minSpawnDistance * minSpawnDistance;

        foreach (var v in vehicles)
        {
            if (v == null) continue;

            Vector3 diff = v.transform.position - position;
            if (diff.sqrMagnitude < minDistSq)
            {
                // Hay un coche muy cerca -> no deberíamos spawnear aquí
                return false;
            }
        }

        // No hay coches cerca -> se puede spawnear
        return true;
    }
}
