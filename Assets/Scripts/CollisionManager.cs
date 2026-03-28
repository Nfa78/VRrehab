using UnityEngine;
using System.Collections.Generic;

public class CollisionManager : MonoBehaviour
{
    private static HashSet<GameObject> collidedObjects = new HashSet<GameObject>();

    public static bool RegisterCollision(GameObject obj)
    {
        if (collidedObjects.Add(obj))
        {
            Debug.Log($"Registered collision with: {obj.name}");
            Debug.Log($"Total unique collisions: {collidedObjects.Count}");
            return true;
        }
        return false;
    }

    public static int GetUniqueCollisionCount()
    {
        return collidedObjects.Count;
    }

    public static void ClearHashSet()
    {
        collidedObjects.Clear();
        Debug.Log("HashSet cleared!");
    }
}
