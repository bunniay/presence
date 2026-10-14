using UnityEngine;

// First-person mouse look with NO movement.
// Left/right turns the whole player. Up/down tilts only the camera,
// so the player's body never tips over.
public class PlayerLook : MonoBehaviour
{
    [Tooltip("The camera, as a CHILD of the player. Leave empty to use the Main Camera.")]
    public Transform playerCamera;

    [Header("Look Settings")]
    public float sensitivity = 2f;
    public float minPitch = -80f;        // how far down you can look
    public float maxPitch = 80f;         // how far up you can look
    public bool invertY = false;

    [Header("Cursor")]
    public bool lockCursor = true;       // hide the cursor and keep it in the window

    private float yaw;                   // left/right angle
    private float pitch;                 // up/down angle

    void Start()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        // Start from whatever direction the player is already facing
        yaw = transform.eulerAngles.y;

        if (lockCursor)
            SetCursorLocked(true);
    }

    void Update()
    {
        // Escape frees the mouse; clicking the game window grabs it again
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLocked(false);
        else if (lockCursor && Input.GetMouseButtonDown(0))
            SetCursorLocked(true);

        // Don't spin the view while the cursor is free (e.g. clicking menus)
        if (lockCursor && Cursor.lockState != CursorLockMode.Locked)
            return;

        // Mouse axes are already "how far the mouse moved this frame",
        // so we do NOT multiply by Time.deltaTime here.
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        yaw += mouseX;
        pitch += invertY ? mouseY : -mouseY;   // mouse up = look up
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);  // no backflips

        if (playerCamera != null && playerCamera != transform)
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);              // body turns left/right
            playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);   // camera tilts up/down
        }
        else
        {
            // Script is on the camera itself: do both rotations here
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
