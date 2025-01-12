using HawkNetworking;
using UnityEngine;
using UnityEngine.UIElements;

namespace CustomItems
{
    public class CustomItem : HawkNetworkBehaviour
    {
        [Header("Basic Information")]
        public string itemName;

        public string itemDescription;

        public Sprite itemSprite;

        [Header("Extra Properties")]
        [InspectorName("Spawn at (Custom spawn position) instead of in front of player")]
        public bool spawnAtPos = false;

        [InspectorName("Custom spawn position")]
        [Tooltip("The name displayed on the custom items page")]
        public Vector3 customSpawnPos = Vector3.zero;
    }
}
