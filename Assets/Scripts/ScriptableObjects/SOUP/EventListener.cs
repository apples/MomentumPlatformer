
using UnityEngine;
using UnityEngine.Events;

namespace SOUP
{

    public class EventListener : MonoBehaviour
    {
        [SerializeField] private Event @event;
        [SerializeField] private UnityEvent<object> response;

        private void OnEnable()
        {
            @event.RegisterListener(OnEventRaised);
        }

        private void OnDisable()
        {
            @event.UnregisterListener(OnEventRaised);
        }

        private void OnEventRaised(object arg)
        {
            response.Invoke(arg);
        }
    }

}
