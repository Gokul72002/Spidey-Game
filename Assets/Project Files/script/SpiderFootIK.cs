using UnityEngine;
using UnityEngine.Animations.Rigging;

public class SpiderFootIK : MonoBehaviour
{
    [Header("Body Reference")]
    public Transform body;

    [Header("IK Constraints")]
    public TwoBoneIKConstraint frontLeftIK;
    public TwoBoneIKConstraint frontRightIK;
    public TwoBoneIKConstraint backLeftIK;
    public TwoBoneIKConstraint backRightIK;

    [Header("Foot Targets")]
    public Transform frontLeftTarget;
    public Transform frontRightTarget;
    public Transform backLeftTarget;
    public Transform backRightTarget;

    [Header("Step Settings")]
    [SerializeField] private float stepDistance = 0.5f;
    [SerializeField] private float stepHeight = 0.3f;
    [SerializeField] private float stepSpeed = 8f;
    [SerializeField] private float footSpacing = 1f;
    [SerializeField] private float legLength = 2f;

    [Header("Terrain Detection")]
    [SerializeField] private LayerMask terrainLayer = 1;
    [SerializeField] private float raycastDistance = 3f;

    [Header("Gait Pattern")]
    private int currentFootIndex = 0; // which foot steps next
    [SerializeField] private bool useAlternatingGait = true;

    [Header("Body Adaptation")]
    [SerializeField] private bool adaptBodyToTerrain = true;
    [SerializeField] private float bodyHeightOffset = 0.5f;
    [SerializeField] private float bodyAdaptSpeed = 2f;
    [SerializeField] private float maxBodyTilt = 15f;

    // Foot data structure
    [System.Serializable]
    public class FootData
    {
        public Transform target;
        public TwoBoneIKConstraint ikConstraint;
        public Vector3 currentPosition;
        public Vector3 oldPosition;
        public Vector3 newPosition;
        public Vector3 footOffset;
        public float lerp;
        public bool isStepping;

        public FootData(Transform target, TwoBoneIKConstraint ik, Vector3 offset)
        {
            this.target = target;
            this.ikConstraint = ik;
            this.footOffset = offset;
            this.lerp = 1f;
            this.isStepping = false;
        }
    }

    private FootData[] feet;
    private int currentSteppingGroup = 0;

    void Start()
    {
        InitializeFeet();
    }

    void InitializeFeet()
    {
        feet = new FootData[4];

        // Initialize foot data with their relative positions to body
        feet[0] = new FootData(frontLeftTarget, frontLeftIK, new Vector3(-footSpacing, 0, footSpacing));
        feet[1] = new FootData(frontRightTarget, frontRightIK, new Vector3(footSpacing, 0, footSpacing));
        feet[2] = new FootData(backLeftTarget, backLeftIK, new Vector3(-footSpacing, 0, -footSpacing));
        feet[3] = new FootData(backRightTarget, backRightIK, new Vector3(footSpacing, 0, -footSpacing));

        // Initialize positions
        for (int i = 0; i < feet.Length; i++)
        {
            Vector3 worldFootPos = body.position + body.TransformDirection(feet[i].footOffset);
            Debug.Log(worldFootPos);
            feet[i].currentPosition = GetGroundPosition(worldFootPos);
            feet[i].oldPosition = feet[i].currentPosition;
            feet[i].newPosition = feet[i].currentPosition;
            feet[i].target.position = feet[i].currentPosition;
        }
    }

    void Update()
    {
        UpdateFootPositions();

        if (adaptBodyToTerrain)
        {
            UpdateBodyPosition();
        }
    }

    void UpdateFootPositions()
    {
        bool anyFootStepping = false;

        for (int i = 0; i < feet.Length; i++)
        {
            FootData foot = feet[i];

            // Calculate desired foot position
            Vector3 desiredWorldPos = body.position + body.TransformDirection(foot.footOffset);
            Vector3 desiredGroundPos = GetGroundPosition(desiredWorldPos);

            // Check if we need to step
            float distanceToTarget = Vector3.Distance(foot.currentPosition, desiredGroundPos);

            if (!foot.isStepping && distanceToTarget > stepDistance)
            {
                // Check gait pattern - only step if it's this foot's turn
                if (CanFootStep(i, anyFootStepping))
                {
                    StartStep(foot, desiredGroundPos);
                }
            }

            // Update step animation
            if (foot.isStepping)
            {
                anyFootStepping = true;
                UpdateStep(foot);
            }

            // Apply position to target
            foot.target.position = foot.currentPosition;
        }
    }

    bool CanFootStep(int footIndex, bool anyFootStepping)
    {
        // Only allow one foot to step at a time
        if (anyFootStepping) return false;

        // Check if it's this foot's turn
        return footIndex == currentFootIndex;
    }


    void StartStep(FootData foot, Vector3 targetPosition)
    {
        foot.isStepping = true;
        foot.lerp = 0f;
        foot.oldPosition = foot.currentPosition;
        foot.newPosition = targetPosition;
    }

    void UpdateStep(FootData foot)
    {
        foot.lerp += Time.deltaTime * stepSpeed;

        if (foot.lerp >= 1f)
        {
            foot.lerp = 1f;
            foot.isStepping = false;
            foot.currentPosition = foot.newPosition;

            // Move to next foot in sequence
            // Order: 0 -> 3 -> 1 -> 2 -> repeat
            int[] sequence = { 0, 3, 1, 2 };
            int currentIndexInSeq = System.Array.IndexOf(sequence, currentFootIndex);
            currentFootIndex = sequence[(currentIndexInSeq + 1) % sequence.Length];
        }

        else
        {
            // Direction and midpoint between start and end
            Vector3 midPoint = (foot.oldPosition + foot.newPosition) * 0.5f;

            // Lift midpoint upwards for the arc
            midPoint += Vector3.up * stepHeight;

            // Hemisphere interpolation
            Vector3 p1 = Vector3.Lerp(foot.oldPosition, midPoint, foot.lerp);
            Vector3 p2 = Vector3.Lerp(midPoint, foot.newPosition, foot.lerp);
            foot.currentPosition = Vector3.Lerp(p1, p2, foot.lerp);
        }
    }


    Vector3 GetGroundPosition(Vector3 worldPosition)
    {
        // Cast ray downward to find ground
        Ray ray = new Ray(worldPosition + Vector3.up * 0.5f, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, terrainLayer))
        {
            return hit.point;
        }

        // Fallback: project down from current position
        return worldPosition - Vector3.up * legLength;
    }

    void UpdateBodyPosition()
    {
        // Calculate average foot height and body orientation
        Vector3 averageFootPosition = Vector3.zero;
        int plantedFeetCount = 0;

        // Only consider feet that are planted (not stepping)
        for (int i = 0; i < feet.Length; i++)
        {
            if (!feet[i].isStepping)
            {
                averageFootPosition += feet[i].currentPosition;
                plantedFeetCount++;
            }
        }

        if (plantedFeetCount > 0)
        {
            averageFootPosition /= plantedFeetCount;

            // Calculate target body height
            float targetBodyHeight = averageFootPosition.y + bodyHeightOffset;

            // Smooth body height adjustment
            Vector3 currentBodyPos = body.position;
            Vector3 targetBodyPos = new Vector3(currentBodyPos.x, targetBodyHeight, currentBodyPos.z);
            body.position = Vector3.Lerp(currentBodyPos, targetBodyPos, Time.deltaTime * bodyAdaptSpeed);
        }

        // Calculate body orientation based on foot positions
        UpdateBodyOrientation();
    }

    void UpdateBodyOrientation()
    {
        // Get positions of planted feet for orientation calculation
        Vector3 frontLeft = feet[0].isStepping ? feet[0].newPosition : feet[0].currentPosition;
        Vector3 frontRight = feet[1].isStepping ? feet[1].newPosition : feet[1].currentPosition;
        Vector3 backLeft = feet[2].isStepping ? feet[2].newPosition : feet[2].currentPosition;
        Vector3 backRight = feet[3].isStepping ? feet[3].newPosition : feet[3].currentPosition;

        // Calculate two vectors to define the plane the spider should align with
        Vector3 frontCenter = (frontLeft + frontRight) * 0.5f;
        Vector3 backCenter = (backLeft + backRight) * 0.5f;
        Vector3 leftCenter = (frontLeft + backLeft) * 0.5f;
        Vector3 rightCenter = (frontRight + backRight) * 0.5f;

        // Forward vector (from back to front)
        Vector3 forwardVector = (frontCenter - backCenter).normalized;

        // Right vector (from left to right)
        Vector3 rightVector = (rightCenter - leftCenter).normalized;

        // Up vector (cross product)
        Vector3 upVector = Vector3.Cross(forwardVector, rightVector).normalized;

        // Create target rotation
        Quaternion targetRotation = Quaternion.LookRotation(forwardVector, upVector);

        // Limit the tilt to prevent extreme orientations
        Vector3 targetEuler = targetRotation.eulerAngles;
        targetEuler.x = ClampAngle(targetEuler.x, -maxBodyTilt, maxBodyTilt);
        targetEuler.z = ClampAngle(targetEuler.z, -maxBodyTilt, maxBodyTilt);
        targetRotation = Quaternion.Euler(targetEuler);

        // Smooth rotation adjustment
        body.rotation = Quaternion.Slerp(body.rotation, targetRotation, Time.deltaTime * bodyAdaptSpeed);
    }

    float ClampAngle(float angle, float min, float max)
    {
        // Convert angle to -180 to 180 range
        if (angle > 180f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }

    void OnDrawGizmos()
    {
        if (feet == null) return;

        // Draw foot positions and targets
        for (int i = 0; i < feet.Length; i++)
        {
            if (feet[i].target != null)
            {
                // Current position
                Gizmos.color = feet[i].isStepping ? Color.yellow : Color.green;
                Gizmos.DrawSphere(feet[i].currentPosition, 0.1f);

                // Target position
                Gizmos.color = Color.red;
                Vector3 desiredPos = body.position + body.TransformDirection(feet[i].footOffset);
                Vector3 groundPos = GetGroundPosition(desiredPos);
                Gizmos.DrawSphere(groundPos, 0.05f);

                // Step threshold
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(feet[i].currentPosition, stepDistance);
            }
        }

        // Draw body direction
        if (body != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(body.position, body.forward * 2f);
        }
    }
}