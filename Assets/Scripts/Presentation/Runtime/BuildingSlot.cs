using UnityEngine;

namespace Richman.Presentation
{
    public sealed class BuildingSlot : MonoBehaviour
    {
        [SerializeField] private int level = 1;

        public int Level => level;

        public void SetLevel(int value)
        {
            level = Mathf.Clamp(value, 1, 3);
        }
    }
}
