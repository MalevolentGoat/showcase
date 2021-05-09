//MMP2B MultiMediaTechnology, FH Salzburg by Matthias Gölzner, Nicolas Vana and Philipp Gewald

//Car Controller for our MultiMediaProject, with quite accurate physics

using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

[System.Serializable]
public class AxleInfo
{
    public WheelCollider leftWheel;
    public GameObject leftWheelVisual;
    public WheelCollider rightWheel;
    public GameObject rightWheelVisual;
    public bool motor;
    public bool steering;
}

public class CarController : MonoBehaviour
{
    [SerializeField] private List<AxleInfo> axleInfos;
    [SerializeField] private float maxMotorTorque;
    [SerializeField] private float maxSteeringAngle;
    [SerializeField] private float maxBreakTorque;
    [SerializeField] private float maxHandbreakTorque;

    private float _steerInput, _accelInput, _brakeInput, _handbrakeInput = 0;
    private List<RaycastHit> _gravityVectors = new List<RaycastHit>();

    [SerializeField] private float gravityForce = 9.81f;
    [SerializeField] private float downforceCoefficient = 1.27f;
    [SerializeField] private float[] gearRatio = new float[5] { 1, 0.8f, 0.6f, 0.4f, 0.3f };
    [SerializeField] private int currentGear = 0;
    [SerializeField] private float upperEngineRPM = 3000.0f;
    [SerializeField] private float lowerEngineRPM = 1000.0f;
    [SerializeField] private float _engineRPM = 0.0f;
    [SerializeField] private float shiftTime = 0.2f;
    private float _curShiftTime = 0;

    private Vector3 _gravity;
    private Rigidbody _rigidbody;
    private Vector3 _centerOfMass;
    private Vector3 _downPressure;

    public void OnSteer(InputAction.CallbackContext ctx) => _steerInput = ctx.ReadValue<float>();
    public void OnAcceleration(InputAction.CallbackContext ctx) => _accelInput = ctx.ReadValue<float>();
    public void OnBrake(InputAction.CallbackContext ctx) => _brakeInput = ctx.ReadValue<float>();
    public void OnHandBrake(InputAction.CallbackContext ctx) => _handbrakeInput = ctx.ReadValue<float>();

    private int _gravityStep = 0;
    [SerializeField] private int _gravitySteps = 10;
    [SerializeField] private int _maxGravitySteps = 10;

    private bool isGrounded = false;
    private Vector3 lastGroundPos;
    [SerializeField] LayerMask roadMask;

    private uint _engineEventID;

    public void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        RaycastHit raycastHit;
        if (Physics.Raycast(transform.position, -transform.up, out raycastHit, 30, roadMask))
        {
            _gravity = -raycastHit.normal * gravityForce;
        } else
        {
            _gravity = -transform.up * gravityForce;
        }
        _centerOfMass = new Vector3(0, GetComponent<BoxCollider>().center.y - GetComponent<BoxCollider>().size.y, 0);
        _rigidbody.centerOfMass = new Vector3(0, GetComponent<BoxCollider>().center.y - GetComponent<BoxCollider>().size.y, 0);

        _engineEventID = AkSoundEngine.PostEvent("Engine", gameObject);
    }

    public void FixedUpdate()
    {
        if (!axleInfos[0].leftWheel.isGrounded && !axleInfos[0].rightWheel.isGrounded && !axleInfos[1].leftWheel.isGrounded && !axleInfos[1].rightWheel.isGrounded)
        {
            isGrounded = false;
        } else {
            isGrounded = true;
            lastGroundPos = transform.position;
        }
        //Wheel Collider
        float motor = maxMotorTorque * gearRatio[currentGear] * _accelInput;
        float steering = maxSteeringAngle * _steerInput;
        float brake = maxBreakTorque * _brakeInput;
        float handbreak = maxHandbreakTorque * _handbrakeInput;

        axleInfos[1].leftWheel.brakeTorque = handbreak;
        axleInfos[1].rightWheel.brakeTorque = handbreak;

        foreach (AxleInfo axleInfo in axleInfos)
        {
            axleInfo.leftWheel.brakeTorque = brake;
            axleInfo.rightWheel.brakeTorque = brake;
            if (axleInfo.steering)
            {
                axleInfo.leftWheel.steerAngle = steering;
                axleInfo.rightWheel.steerAngle = steering;
            }
            if (axleInfo.motor)
            {
                if (_curShiftTime > 0)
                {
                    _engineRPM = 0;
                    _curShiftTime -= Time.deltaTime;
                    axleInfo.leftWheel.motorTorque = 0;
                    axleInfo.rightWheel.motorTorque = 0;
                }
                else
                {
                    _engineRPM = gearRatio[currentGear] * (axleInfo.rightWheel.rpm + axleInfo.leftWheel.rpm) / 2;
                    if (_engineRPM >= upperEngineRPM && currentGear < gearRatio.Length - 1)
                    {
                        currentGear++;
                        _curShiftTime = shiftTime;
                        AkSoundEngine.PostEvent("Gear_Shift", gameObject);
                    }
                    else if (_engineRPM <= lowerEngineRPM && currentGear > 0)
                    {
                        currentGear--;
                        _curShiftTime = shiftTime;
                        AkSoundEngine.PostEvent("Gear_Shift", gameObject);
                    }
                    if (_engineRPM > upperEngineRPM * 1.2f)
                    {
                        axleInfo.leftWheel.brakeTorque = maxBreakTorque * 2;
                        axleInfo.rightWheel.brakeTorque = maxBreakTorque * 2;
                    }
                    axleInfo.leftWheel.motorTorque = motor;
                    axleInfo.rightWheel.motorTorque = motor;
                }
                //Audio
                float value = RemapClamp(0, 100, 0, upperEngineRPM, _engineRPM);
                AkSoundEngine.SetRTPCValueByPlayingID("Acelleration", value, _engineEventID);
            }
            ApplyLocalPositionToVisuals(axleInfo.leftWheelVisual, axleInfo.leftWheel);
            ApplyLocalPositionToVisuals(axleInfo.rightWheelVisual, axleInfo.rightWheel);
        }
        

        //Gravity
        if (_gravityStep >= _gravitySteps)
        {
            _gravityStep = 0;
            if (_gravityVectors.Count >= _maxGravitySteps)
            {
                _gravityVectors.RemoveAt(0);
            }

            _gravityVectors.Add(getBestGravityRay());

            Vector3 localGravity = new Vector3();
            int i = 1;
            foreach (RaycastHit gravVec in _gravityVectors)
            {
                localGravity += -gravVec.normal * i;
                i++;
            }
            _gravity = localGravity.normalized * gravityForce;
        }
        else { _gravityStep++; }

        _rigidbody.AddForce(_gravity, ForceMode.Acceleration); //Don't use deltaTime here!
        //downforce
        float forwardVel = Vector3.Project(_rigidbody.velocity, transform.forward).magnitude;
        _downPressure = -transform.up * downforceCoefficient * forwardVel * forwardVel * Time.deltaTime;
        _rigidbody.AddForce(_downPressure);

        _rigidbody.AddForce(-transform.forward * (forwardVel * forwardVel) * Time.deltaTime);

        //rotation to gravity
        if(!isGrounded)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(transform.forward, -_gravity.normalized), Time.deltaTime);
        }
    }

    public void OnDrawGizmos()
    {
        Debug.DrawRay(transform.position - _centerOfMass - new Vector3(0.1f, 0), _gravity, Color.green);
        Debug.DrawRay(transform.position - _centerOfMass + new Vector3(0.1f, 0), _downPressure, Color.cyan);
        foreach(var gravVec in _gravityVectors)
        {
            Debug.DrawRay(gravVec.point, -gravVec.normal, Color.red);
        }
    }

    private void ApplyLocalPositionToVisuals(GameObject wheelVisual, WheelCollider collider)
    {
        Transform visualWheel = wheelVisual.transform;

        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);

        visualWheel.transform.position = position;
        visualWheel.transform.rotation = rotation;
    }

    private RaycastHit getBestGravityRay()
    {
        RaycastHit raycastHitDown;
        RaycastHit raycastHitForward;

        Physics.Raycast(transform.position, transform.forward, out raycastHitForward, 50, roadMask);
        Physics.Raycast(transform.position, -transform.up, out raycastHitDown, 50, roadMask);

        if (raycastHitDown.collider != null && raycastHitForward.collider != null)
        {
            return raycastHitDown.distance < raycastHitForward.distance ? raycastHitDown : raycastHitForward;
        } else if(raycastHitDown.collider != null)
        {
            return raycastHitDown;
        } else if(raycastHitForward.collider != null)
        {
            return raycastHitForward;
        }
        RaycastHit raycastHitLeft;
        RaycastHit raycastHitRight;

        Physics.Raycast(transform.position, transform.right, out raycastHitRight, 50, roadMask);
        Physics.Raycast(transform.position, -transform.right, out raycastHitLeft, 50, roadMask);

        if (raycastHitLeft.collider != null && raycastHitRight.collider != null)
        {
            return raycastHitLeft.distance < raycastHitRight.distance ? raycastHitLeft : raycastHitRight;
        }
        else if (raycastHitLeft.collider != null)
        {
            return raycastHitLeft;
        }
        else if (raycastHitRight.collider != null)
        {
            return raycastHitRight;
        }

        raycastHitDown.normal = (transform.position - lastGroundPos).normalized / (_maxGravitySteps*4);
        raycastHitDown.point = lastGroundPos;
        return raycastHitDown;
    }

    private float RemapClamp(float min, float max, float minValue, float maxValue, float value)
    {
        value = Mathf.Clamp(value, minValue, maxValue);
        value = value / (maxValue - minValue);
        value = value * (max - min);
        return value;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Audio
        float minValue = 0;
        float maxValue = 10000;
        float value = RemapClamp(0, 100, minValue, maxValue, collision.impulse.magnitude);
        uint eventId = AkSoundEngine.PostEvent("Crash", gameObject);
        AkSoundEngine.SetRTPCValueByPlayingID("CarCrash", value, eventId);
    }
}