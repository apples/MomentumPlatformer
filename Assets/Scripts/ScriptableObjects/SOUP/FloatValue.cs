
using UnityEngine;

namespace SOUP
{

    [CreateAssetMenu(menuName = "SOUP/Values/Float")]
    public class FloatValue : ScriptableObject
    {
        [SerializeField] private float initialValue;

        public delegate void EventDelegate(FloatValue value);

        private EventDelegate listeners;

        private float value;
        public float Value
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

