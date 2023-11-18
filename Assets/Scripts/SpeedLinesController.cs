using System.Collections;
using System.Collections.Generic;
using SOUP;
using UnityEngine;
using UnityEngine.VFX;

public class SpeedLines : MonoBehaviour
{
    public FloatValue currentSpeed;
    public VisualEffect effect;

    public float speedThreshold = 200;

    private int radiusID;
    private int maxAlphaID;

    // Start is called before the first frame update
    void Start()
    {
        radiusID = Shader.PropertyToID("Radius");
        maxAlphaID = Shader.PropertyToID("MaxAlpha");
    }

    // Update is called once per frame
    void Update()
    {
        effect.SetFloat(radiusID, Mathf.Max(5 - ((currentSpeed.Value * 3.6f) / speedThreshold), 2));
        effect.SetFloat(maxAlphaID, ((currentSpeed.Value * 3.6f) - speedThreshold) / (speedThreshold * 5));
    }
}
