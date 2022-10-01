
using UnityEngine;

namespace SOUP
{

    [CreateAssetMenu(menuName = "SOUP/Event")]
    public class Event : ScriptableObject
    {
        public delegate void EventDelegate(object arg);

        private EventDelegate listeners;

        public void Raise(object arg = null)
        {
            if (listeners != null)
            {
                listeners(arg);
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
