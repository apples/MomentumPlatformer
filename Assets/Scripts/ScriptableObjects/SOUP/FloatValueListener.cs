
using UnityEngine;
using UnityEngine.Events;

namespace SOUP
{

    public class FloatValueListener : MonoBehaviour
    {
        [SerializeField] private FloatValue value;
        [SerializeField] private UnityEvent<float> response;

        private void OnEnable()
        {
            value.RegisterListener(OnEventRaised);
        }

        private void OnDisable()
        {
            value.UnregisterListener(OnEventRaised);
        }

        private void OnEventRaised(FloatValue arg)
        {
            response.Invoke(arg.Value);
        }
    }

}
