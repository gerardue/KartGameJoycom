using UnityEngine;
using UnityEngine.Assertions;


public class KartPlayerAnimator : MonoBehaviour
{
    public Animator PlayerAnimator;
    public KartController Kart;

    public string SteeringParam = "Steering";

    int steerHash; 

    float steeringSmoother;

    void Awake()
    {
        steerHash  = Animator.StringToHash(SteeringParam);
    }

    void Update()
    {
        steeringSmoother = Mathf.Lerp(steeringSmoother, Kart.Input.x, Time.deltaTime * 5f);
        PlayerAnimator.SetFloat(steerHash, steeringSmoother);

    }
}

