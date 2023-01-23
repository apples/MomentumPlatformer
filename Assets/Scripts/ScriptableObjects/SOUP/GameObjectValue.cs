
using UnityEngine;

namespace SOUP
{

    [CreateAssetMenu(menuName = "SOUP/Values/GameObject")]
    public class GameObjectValue : ScriptableObject
    {
        [SerializeField] private GameObject initialValue;
        [SerializeField] private bool dontUnloadWhenUnused;

        public delegate void EventDelegate(GameObjectValue value);

        private EventDelegate listeners;

        private GameObject value;
        public GameObject Value
        {
            get { return value; }
            set
            {
                this.value = value;
                if (listeners != null)
                {
                    listeners(this);
                }
            }
        }

        private void OnEnable()
        {
            value = initialValue;
            if (dontUnloadWhenUnused)
            {
                hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
        }

        public void RegisterListener(EventDelegate listener)
        {
            listeners += listener;
        }

        public void UnregisterListener(EventDelegate listener)
        {
            listeners -= listener;
        }
    }

}

