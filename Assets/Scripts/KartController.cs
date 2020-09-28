using UnityEngine;
using System.Collections.Generic;

public class KartController : MonoBehaviour
{
    [System.Serializable]
    public struct Stats
    {
        [Header("Movement Settings")]
        [Tooltip("The maximum speed forwards")]
        public float TopSpeed;

        [Tooltip("How quickly the Kart reaches top speed.")]
        public float Acceleration;

        [Tooltip("The maximum speed backward.")]
        public float ReverseSpeed;

        [Tooltip("The rate at which the kart increases its backward speed.")]
        public float ReverseAcceleration;

        [Tooltip("How quickly the Kart starts accelerating from 0. A higher number means it accelerates faster sooner.")]
        [Range(0.2f, 1)]
        public float AccelerationCurve;

        [Tooltip("How quickly the Kart slows down when going in the opposite direction.")]
        public float Braking;

        [Tooltip("How quickly to slow down when neither acceleration or reverse is held.")]
        public float CoastingDrag;

        [Range(0, 1)]
        [Tooltip("The amount of side-to-side friction.")]
        public float Grip;

        [Tooltip("How quickly the Kart can turn left and right.")]
        public float Steer;

        [Tooltip("Additional gravity for when the Kart is in the air.")]
        public float AddedGravity;

        [Tooltip("How much the Kart tries to keep going forward when on bumpy terrain.")]
        [Range(0, 1)]
        public float Suspension;
    }

    public Rigidbody Rigidbody; 
    public Vector2 Input       { get; private set; }
    public float AirPercent    { get; private set; }
    public float GroundPercent { get; private set; }

    public Stats baseStats = new Stats
    {
        TopSpeed            = 10f,
        Acceleration        = 5f,
        AccelerationCurve   = 4f,
        Braking             = 10f,
        ReverseAcceleration = 5f,
        ReverseSpeed        = 5f,
        Steer               = 5f,
        CoastingDrag        = 4f,
        Grip                = .95f,
        AddedGravity        = 1f,
        Suspension          = .2f
    };

    [Header("Vehicle Physics")]
    [Tooltip("The transform that determines the position of the Kart's mass.")]
    public Transform CenterOfMass;

    [Tooltip("The physical representations of the Kart's wheels.")]
    public Transform[] Wheels;

    [Tooltip("How far to raycast when checking for ground.")]
    public float RaycastDist = 0.3f;

    // the input sources that can control the kart
    public KeyboardInputControl[] inputControls;

    // can the kart move?
    bool canMove = true;
    Stats finalStats;

    void Awake()
    {
        SetConfigurationKart();
    }

    void FixedUpdate()
    {
        GatherInputs();

        Rigidbody.centerOfMass = Rigidbody.transform.InverseTransformPoint(CenterOfMass.position);

        int groundedCount = WheelsOverGround();
        GroundPercent = (float)groundedCount / Wheels.Length;

        float accel = Input.y;
        float turn = Input.x;

        if (canMove)
            MoveVehicle(accel, turn);
    }

    void GatherInputs()
    {
        Input = Vector2.zero;

        for (int i = 0; i < inputControls.Length; i++)
        {
            var inputSource = inputControls[i];
            Vector2 current = inputSource.CreatingInput(); 
            if (current.sqrMagnitude > 0)
            {
                Input = current;
            }
        }
    }

    void SetConfigurationKart()
    {
        finalStats = baseStats;

        finalStats.Grip = Mathf.Clamp(finalStats.Grip, 0, 1);
        finalStats.Suspension = Mathf.Clamp(finalStats.Suspension, 0, 1);
    }

    int WheelsOverGround()
    {
        int groundedCount = 0;

        for (int i = 0; i < Wheels.Length; i++)
        {
            Transform current = Wheels[i];
            groundedCount += Physics.Raycast(current.position, Vector3.down, out RaycastHit hit, RaycastDist) ? 1 : 0;
        }

        return groundedCount;
    }

    void MoveVehicle(float accelInput, float turnInput)
    {
        // acceleration curve coefficient scalar
        float accelerationCurveCoeff = 5;
        Vector3 localVel = transform.InverseTransformVector(Rigidbody.velocity);

        bool isAcceleratingFoward = accelInput >= 0;
        bool isVelocityDirecctionForward = localVel.z >= 0;

        // use the max speed for the direction we are going--forward or reverse.
        float maxSpeed = isAcceleratingFoward ? finalStats.TopSpeed : finalStats.ReverseSpeed;
        float accelPower = isAcceleratingFoward ? finalStats.Acceleration : finalStats.ReverseAcceleration;

        float accelRampTime = Rigidbody.velocity.magnitude / maxSpeed;
        float multipliedAccelerationCurve = finalStats.AccelerationCurve * accelerationCurveCoeff;
        float accelRamp = Mathf.Lerp(multipliedAccelerationCurve, 1, accelRampTime * accelRampTime);

        bool isBraking = isAcceleratingFoward != isVelocityDirecctionForward;

        // if we are braking (moving reverse to where we are going)
        // use the braking accleration instead
        float finalAccelPower = isBraking ? finalStats.Braking : accelPower;

        float finalAcceleration = finalAccelPower * accelRamp;

        // apply inputs to forward/backward
        float turningPower = turnInput * finalStats.Steer;

        Quaternion turnAngle = Quaternion.AngleAxis(turningPower, Rigidbody.transform.up);
        Vector3 forward = turnAngle * Rigidbody.transform.forward;

        Vector3 movement = forward * accelInput * finalAcceleration * GroundPercent;

        Vector3 adjustedVelocity = Rigidbody.velocity + movement * Time.deltaTime;

        adjustedVelocity.y = Rigidbody.velocity.y;

        Rigidbody.velocity = adjustedVelocity;

        if (GroundPercent > 0)
        {
            AddingVelocityAndMovementToKart(isVelocityDirecctionForward, isAcceleratingFoward, turningPower);
            ApplyFriction(forward);
        }
    }

    void AddingVelocityAndMovementToKart(bool pIsVelocityToForward, bool pIsAccelerateToForward, float pTurningPower)
    {
        float angularVelocitySteering = .4f;
        float angularVelocitySmoothSpeed = 20f;

        InvertTurningIfReverse(pIsVelocityToForward, pIsAccelerateToForward, ref angularVelocitySteering);

        AddingAngularVelocity(angularVelocitySteering, angularVelocitySmoothSpeed, pTurningPower);
    }

    void InvertTurningIfReverse(bool pIsVelocityToForward, bool pIsAccelerateToForward, ref float pAngularVelocitySteering)
    {
        if (!pIsVelocityToForward && !pIsAccelerateToForward) 
            pAngularVelocitySteering *= -1;
    }

    void AddingAngularVelocity(float pAngularVelocitySteering, float pAngularVelocitySmoothSpeed, float pTurningPower)
    {
        var angularVel = Rigidbody.angularVelocity;
        angularVel.y = Mathf.MoveTowards(angularVel.y, pTurningPower * pAngularVelocitySteering, Time.deltaTime * pAngularVelocitySmoothSpeed);

        Rigidbody.angularVelocity = angularVel;
    }

    void ApplyFriction(Vector3 pForward)
    {
        float gripCoefficient = 30f;
        Vector3 latFrictionDirection = Vector3.Cross(pForward, transform.up);
        float latSpeed = Vector3.Dot(Rigidbody.velocity, latFrictionDirection);
        Vector3 latFrictionDampedVelocity = Rigidbody.velocity - latFrictionDirection * latSpeed * finalStats.Grip * gripCoefficient * Time.deltaTime;

        Rigidbody.velocity = latFrictionDampedVelocity;
    }

    public float LocalSpeed()
    {
        if (canMove)
        {
            float dot = Vector3.Dot(transform.forward, Rigidbody.velocity);
            if (Mathf.Abs(dot) > 0.1f)
            {
                float speed = Rigidbody.velocity.magnitude;
                return dot < 0 ? -(speed / finalStats.ReverseSpeed) : (speed / finalStats.TopSpeed);
            }
            return 0f;
        }
        else
        {
            return Input.y;
        }
    }

    public void SetCanMove(bool move)
    {
        canMove = move;
    }
}