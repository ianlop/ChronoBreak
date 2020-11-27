﻿using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] Transform playerCam;
    [SerializeField] Transform orientation;
    private Rigidbody rb;

    //Rotation and look
    private float xRotation;
    private float sensitivity = 50f;
    private float sensMultiplier = 1f;
    
    //Movement
    [Header("Movement Settings")]
    [SerializeField] float moveSpeed = 4500;
    [SerializeField] float maxSpeed = 20;

    [SerializeField] bool grounded;
    [SerializeField] LayerMask whatIsGround;
    
    [SerializeField] float counterMovement = 0.175f;
    private float threshold = 0.01f;
    [SerializeField] float maxSlopeAngle = 35f;

    //Crouch & Slide
    [Header("Sliding Settings")]
    [SerializeField] float slideForce = 400;
    [SerializeField] float slideCounterMovement = 0.2f;
    private Vector3 crouchScale = new Vector3(1, 0.5f, 1);
    private Vector3 playerScale;
    private Vector3 normalVector = Vector3.up;
    private Vector3 wallNormalVector;

    //Jumping
    [Header("Jumping Settings")] 
    [SerializeField] float jumpForce = 550f;
    private bool readyToJump = true;
    private const float jumpCooldown = 0.25f;

    private bool cancellingGrounded;

    //Input References
    private float x, y;
    private bool jumping, sprinting, crouching;
    

    //Dashing
    private static Vector3 orientationDirection;
    private GameObject obj;

    //Resetting
    private Vector3 lastSafePos;
    
    void Start() 
    {
        rb = GetComponent<Rigidbody>();
        playerScale =  transform.localScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        obj = GameObject.Find("test");
    }

    
    void FixedUpdate() 
    {
        Movement();
        if (grounded) 
        {
            SavePos();
        }
    }

    void Update() 
    {
        MyInput();
        Look();
        orientationDirection = orientation.transform.forward;
        Debug.DrawRay(transform.position, playerCam.forward * 8, Color.green);
        // print(rb.velocity.magnitude);
    }

    // Handling user input
    private void MyInput() 
    {
        x = Input.GetAxisRaw("Horizontal");
        y = Input.GetAxisRaw("Vertical");
        jumping = Input.GetButton("Jump");
        crouching = Input.GetKey(KeyCode.LeftControl);
      
        //Crouching
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            StartCrouch();
        }
        if (Input.GetKeyUp(KeyCode.LeftControl))
        {
            StopCrouch();
        }
    }

    private void StartCrouch() 
    {
        // Scales the player down
        transform.localScale = crouchScale;
        transform.position = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);
        if (rb.velocity.magnitude > 0.5f) 
        {
            if (grounded) 
            {
                rb.AddForce(orientation.transform.forward * slideForce);
            }
        }
    }

    private void StopCrouch() 
    {
        // Scales player back to normal
        transform.localScale = playerScale;
        transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
    }

    private void Movement() 
    {
        // Extra gravity
        rb.AddForce(Vector3.down * Time.deltaTime * 100);
        
        // Find actual velocity relative to where player is looking
        Vector2 mag = FindVelRelativeToLook();
        float xMag = mag.x, yMag = mag.y;

        // Counteract sliding and sloppy movement
        CounterMovement(x, y, mag);
        
        // If holding jump && ready to jump, then jump
        if (jumping) 
        {
            Jump();
        }

        // Set max speed
        // float maxSpeed = this.maxSpeed;
        
        // If sliding down a ramp, add force down so player stays grounded and also builds speed
        if (crouching && grounded && readyToJump) 
        {
            rb.AddForce(Vector3.down * Time.deltaTime * 3000);
            return;
        }
        
        // If speed is larger than maxspeed, cancel out the input so you don't go over max speed
        if (x > 0 && xMag > maxSpeed || x < 0 && xMag < -maxSpeed) 
        {
            x = 0;
        }
        if (y > 0 && yMag > maxSpeed || y < 0 && yMag < -maxSpeed) 
        {
            y = 0;
        }

        // Some multipliers
        float multiplier = 1f, multiplierV = 1f;
        
        // Movement in air
        if (!grounded) 
        {
            multiplier = 0.5f;
            multiplierV = 0.5f;
        }
        
        // Movement while sliding
        if (grounded && crouching) 
        {
            multiplierV = 0f;
        }

        // Apply forces to move player
        rb.AddForce(orientation.transform.forward * y * moveSpeed * Time.deltaTime * multiplier * multiplierV);
        rb.AddForce(orientation.transform.right * x * moveSpeed * Time.deltaTime * multiplier);
    }

    private void Jump() 
    {
        if (grounded && readyToJump) 
        {
            readyToJump = false;

            // Add jump forces
            rb.AddForce(Vector2.up * jumpForce * 1.5f);
            // rb.AddForce(normalVector * jumpForce * 0.5f);
            
            // If jumping while falling, reset y velocity.
            Vector3 vel = rb.velocity;
            if (rb.velocity.y < 0.5f)
            {
                rb.velocity = new Vector3(vel.x, 0, vel.z);
            }
            else if (rb.velocity.y > 0) 
            {
                rb.velocity = new Vector3(vel.x, vel.y / 2, vel.z);
            }
            
            Invoke(nameof(ResetJump), jumpCooldown);
        }
    }
    
    private void ResetJump() 
    {
        readyToJump = true;
    }
    
    private float desiredX;
    private void Look() 
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.fixedDeltaTime * sensMultiplier;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.fixedDeltaTime * sensMultiplier;

        // Find current look rotation
        Vector3 rot = playerCam.transform.localRotation.eulerAngles;
        desiredX = rot.y + mouseX;
        
        // Rotate, and also make sure we dont over- or under-rotate.
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Perform the rotations
        playerCam.transform.localRotation = Quaternion.Euler(xRotation, desiredX, 0);
        orientation.transform.localRotation = Quaternion.Euler(0, desiredX, 0);
    }

    private void CounterMovement(float x, float y, Vector2 mag) 
    {
        if (!grounded || jumping) 
        {
            return;
        }

        // Slow down sliding
        if (crouching) 
        {
            rb.AddForce(moveSpeed * Time.deltaTime * -rb.velocity.normalized * slideCounterMovement);
            return;
        }

        // Counter movement
        if (Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) || (mag.x > threshold && x < 0)) 
        {
            rb.AddForce(moveSpeed * orientation.transform.right * Time.deltaTime * -mag.x * counterMovement);
        }
        if (Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) || (mag.y > threshold && y < 0)) 
        {
            rb.AddForce(moveSpeed * orientation.transform.forward * Time.deltaTime * -mag.y * counterMovement);
        }
        
    }


    /// Find the velocity relative to where the player is looking
    /// Useful for vectors calculations regarding movement and limiting movement
    public Vector2 FindVelRelativeToLook() 
    {
        float lookAngle = orientation.transform.eulerAngles.y;
        float moveAngle = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg;

        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;

        float magnitue = rb.velocity.magnitude;
        float yMag = magnitue * Mathf.Cos(u * Mathf.Deg2Rad);
        float xMag = magnitue * Mathf.Cos(v * Mathf.Deg2Rad);
        
        return new Vector2(xMag, yMag);
    }

    private bool IsFloor(Vector3 v) 
    {
        float angle = Vector3.Angle(Vector3.up, v);
        return angle < maxSlopeAngle;
    }
    
    private void OnCollisionStay(Collision other) 
    {
        // Make sure we are only checking for walkable layers
        int layer = other.gameObject.layer;
        if (whatIsGround != (whatIsGround | (1 << layer))) 
        {
            return;
        }

        // Iterate through every collision in a physics update
        for (int i = 0; i < other.contactCount; i++) 
        {
            Vector3 normal = other.contacts[i].normal;
            // FLOOR
            if (IsFloor(normal)) 
            {
                grounded = true;
                cancellingGrounded = false;
                normalVector = normal;
                CancelInvoke(nameof(StopGrounded));
            }
        }

        // Invoke ground/wall cancel, since we can't check normals with CollisionExit
        float delay = 3f;
        if (!cancellingGrounded) 
        {
            cancellingGrounded = true;
            Invoke(nameof(StopGrounded), Time.deltaTime * delay);
        }
    }

    private void StopGrounded() 
    {
        grounded = false;
    }

    public Vector3 getOrientationDirection()
    {
        //returns the orientation forward
        return orientationDirection;
    }

    //getPlayerCameraForward
    public Transform getPlayerCam()
    {
        return playerCam;
    }

    public bool isGrounded()
    {
        //returns the grounded bool variable
        return grounded;
    }

    private void SavePos()
    {
        lastSafePos = transform.position;
    }

    public void LoadSafePos()
    {
        transform.position = lastSafePos;
        rb.velocity = Vector3.zero;
    }

}
