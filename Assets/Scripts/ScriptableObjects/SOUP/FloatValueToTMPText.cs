
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace SOUP
{

    public class FloatValueToTMPText : MonoBehaviour
    {
        [SerializeField] private FloatValue value;

        [SerializeField] private TMP_Text tmpText;

        [SerializeField] private string format = "0.00";

        private void OnEnable()
        {
            value.RegisterListener(OnEventRaised);
            tmpText.text = value.Value.ToString(format);
        }

        private void OnDisable()
        {
            value.UnregisterListener(OnEventRaised);
        }

        private void OnEventRaised(FloatValue arg)
        {
            tmpText.text = value.Value.ToString(format);
        }
    }

}
