
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace SOUP
{

    public class RaiseEvent : MonoBehaviour
    {
        [SerializeField] private Event @event;

        public void Raise()
        {
            @event.Raise();
        }
    }

}
