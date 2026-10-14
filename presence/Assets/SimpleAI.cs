using System.Collections.Generic;   // for List<>
using UnityEngine;

// This script is a "mood controller": it picks a random emotion every few
// seconds, and each emotion does something to the objects in the list.
// The darker the room, the faster moods change and the faster objects move.
public class SimpleAI : MonoBehaviour
{
    // Idle is added at the END on purpose: Unity saves enums as numbers,
    // so adding it in the middle would shift the saved values of the others.
    public enum Emotion { Happy, Distraught, Sad, Angry, Aloof, Idle }

    [Header("Allowed Emotions")]
    [Tooltip("Untick an emotion and this object will never feel it.")]
    public bool canBeHappy = true;
    public bool canBeDistraught = true;
    public bool canBeSad = true;
    public bool canBeAngry = true;
    public bool canBeAloof = true;
    public bool canBeIdle = true;

    [Header("Timing (seconds)")]
    public float minWaitTime = 15f;
    public float maxWaitTime = 45f;

    [Header("Idle Timing (seconds)")]
    [Tooltip("Always start the game in Idle, even if Can Be Idle is unticked.")]
    public bool startInIdle = true;
    [Tooltip("Idle uses its own wait time instead of Min/Max Wait Time.")]
    public float idleMinWaitTime = 30f;
    public float idleMaxWaitTime = 30f;

    [Header("Darkness")]
    [Tooltip("Leave empty to find the RoomLighting in the scene automatically.")]
    public RoomLighting roomLighting;
    [Tooltip("In full darkness, wait times are multiplied by this. 0.25 = moods change 4x as often.")]
    [Range(0.05f, 1f)] public float darkWaitMultiplier = 2f;
    [Tooltip("In full darkness, movement speeds are multiplied by this. 2 = twice as fast.")]
    public float darkSpeedMultiplier = 0.5f;

    [Header("Affected Objects")]
    [Tooltip("The objects each emotion acts on. Leave empty to use this object.")]
    public Transform[] objects;

    [Header("Player")]
    public Transform player;             // drag the player here (needed for Angry + Distraught)

    [Header("Happy: wander")]
    public float wanderSpeed = 0.5f;
    public float wanderRadius = 1f;

    [Header("Angry: approach player")]
    public float approachSpeed = 100f;
    public float stopDistance = 5f;      // don't pile right on top of the player

    [Header("Distraught: flee player")]
    public float fleeSpeed = 100f;
    public float maxFleeDistance = 8f;   // stop fleeing once this far away

    [Header("Aloof: sounds")]
    [Tooltip("Fallback sounds. Objects whose Audio Source already has a clip play that instead.")]
    public AudioClip[] aloofSounds;

    public Emotion currentState;

    private float timer;
    private float waitTime;

    // One entry per affected object (same index as the objects array)
    private Vector3[] objectHomes;        // where each object started
    private Vector3[] objectWanderPoints; // where each object is walking to
    private Renderer[][] objectRenderers; // each object's renderers (incl. children), switched off to hide it
    private AudioSource[] objectAudio;    // so each object plays its own sound

    // ---- Darkness helpers ----
    // "=>" makes these read-only values that are recalculated every time you use them.
    private float Darkness => roomLighting != null ? roomLighting.Darkness : 0f;
    private float WaitMultiplier => Mathf.Lerp(1f, darkWaitMultiplier, Darkness);
    private float SpeedMultiplier => Mathf.Lerp(1f, darkSpeedMultiplier, Darkness);

    void Awake()
    {
        if (roomLighting == null)
            roomLighting = FindFirstObjectByType<RoomLighting>();

        // No player assigned? Find the object tagged "Player".
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }

        // Nothing assigned? Then this object affects itself.
        if (objects == null || objects.Length == 0)
            objects = new Transform[] { transform };

        objectHomes = new Vector3[objects.Length];
        objectWanderPoints = new Vector3[objects.Length];
        objectRenderers = new Renderer[objects.Length][];
        objectAudio = new AudioSource[objects.Length];

        for (int i = 0; i < objects.Length; i++)
        {
            objectHomes[i] = objects[i].position;

            // Renderer covers SpriteRenderer, MeshRenderer, etc.
            // InChildren also catches models whose visible parts are child objects.
            objectRenderers[i] = objects[i].GetComponentsInChildren<Renderer>();

            // Use the object's AudioSource, or give it one if it doesn't have one
            objectAudio[i] = objects[i].GetComponent<AudioSource>();
            if (objectAudio[i] == null)
                objectAudio[i] = objects[i].gameObject.AddComponent<AudioSource>();
        }
    }

    void Start()
    {
        List<Emotion> allowed = GetAllowedEmotions();

        // Every box unticked? Nothing to do, so switch this script off.
        if (allowed.Count == 0)
        {
            Debug.LogWarning(name + ": SimpleAI has no emotions ticked, so it's turned off.", this);
            enabled = false;   // stops Update from running
            return;
        }

        // Start in Idle, or in a random allowed emotion
        if (startInIdle)
            currentState = Emotion.Idle;
        else
            currentState = allowed[Random.Range(0, allowed.Count)];

        waitTime = RollWaitTime(currentState);
        OnEnterState(currentState);
    }

    // How long a mood lasts. Idle has its own (longer) time; everything else uses Min/Max.
    private float RollWaitTime(Emotion state)
    {
        if (state == Emotion.Idle)
            return Random.Range(idleMinWaitTime, idleMaxWaitTime);

        return Random.Range(minWaitTime, maxWaitTime);
    }

    void Update()
    {
        timer += Time.deltaTime;

        // ---- PER-FRAME behavior (runs every frame while in the state) ----
        switch (currentState)
        {
            case Emotion.Happy: UpdateHappy(); break;
            case Emotion.Distraught: UpdateDistraught(); break;
            case Emotion.Sad:        /* hidden, nothing to do */      break;
            case Emotion.Angry: UpdateAngry(); break;
            case Emotion.Aloof:      /* sound already played */       break;
            case Emotion.Idle:       /* standing by, does nothing */  break;
        }

        // ---- Time to switch moods? ----
        // The multiplier is checked every frame, so if the room gets darker
        // mid-wait, the current mood ends sooner right away.
        if (timer > waitTime * WaitMultiplier)
        {
            timer = 0;
            waitTime = RollWaitTime(currentState);   // replaced below if the mood changes

            // Pick from the allowed emotions, minus the one we're already in
            List<Emotion> choices = GetAllowedEmotions();
            choices.Remove(currentState);

            // If only one emotion is allowed, there's nothing to switch to: stay in it
            if (choices.Count > 0)
                ChangeState(choices[Random.Range(0, choices.Count)]);
        }
    }

    // Builds a list of the emotions whose box is ticked.
    // Checked every time, so you can tick/untick boxes while the game is running.
    private List<Emotion> GetAllowedEmotions()
    {
        List<Emotion> allowed = new List<Emotion>();
        if (canBeHappy) allowed.Add(Emotion.Happy);
        if (canBeDistraught) allowed.Add(Emotion.Distraught);
        if (canBeSad) allowed.Add(Emotion.Sad);
        if (canBeAngry) allowed.Add(Emotion.Angry);
        if (canBeAloof) allowed.Add(Emotion.Aloof);
        if (canBeIdle) allowed.Add(Emotion.Idle);
        return allowed;
    }

    // ================= STATE CHANGES =================

    private void ChangeState(Emotion newState)
    {
        OnExitState(currentState);   // clean up the old mood
        currentState = newState;
        waitTime = RollWaitTime(newState);  // Idle gets its long wait, others the normal one
        OnEnterState(newState);      // set up the new mood
    }

    // ONE-TIME stuff when a mood starts
    private void OnEnterState(Emotion state)
    {
        switch (state)
        {
            case Emotion.Happy:
                for (int i = 0; i < objects.Length; i++)
                    PickNewWanderPoint(i);
                break;

            case Emotion.Sad:
                SetObjectsVisible(false);
                break;

            case Emotion.Aloof:
                PlayObjectSounds();
                break;
        }
    }

    // ONE-TIME cleanup when a mood ends, so nothing "leaks" into the next mood
    private void OnExitState(Emotion state)
    {
        switch (state)
        {
            case Emotion.Sad:
                SetObjectsVisible(true);
                break;
        }
    }

    // ================= PER-FRAME BEHAVIORS =================

    // HAPPY: each object wanders around its own starting spot
    private void UpdateHappy()
    {
        float speed = wanderSpeed * SpeedMultiplier;

        for (int i = 0; i < objects.Length; i++)
        {
            objects[i].position = Vector3.MoveTowards(
                objects[i].position, objectWanderPoints[i], speed * Time.deltaTime);

            if (Vector3.Distance(objects[i].position, objectWanderPoints[i]) < 0.05f)
                PickNewWanderPoint(i);
        }
    }

    // ANGRY: each object moves toward the player
    private void UpdateAngry()
    {
        if (player == null) return;

        float speed = approachSpeed * SpeedMultiplier;

        for (int i = 0; i < objects.Length; i++)
        {
            if (Vector3.Distance(objects[i].position, player.position) <= stopDistance)
                continue; // close enough, skip to the next object

            objects[i].position = Vector3.MoveTowards(
                objects[i].position, player.position, speed * Time.deltaTime);
        }
    }

    // DISTRAUGHT: each object moves away from the player
    private void UpdateDistraught()
    {
        if (player == null) return;

        float speed = fleeSpeed * SpeedMultiplier;

        for (int i = 0; i < objects.Length; i++)
        {
            Vector3 awayFromPlayer = objects[i].position - player.position;

            if (awayFromPlayer.magnitude >= maxFleeDistance)
                continue; // far enough, skip to the next object

            awayFromPlayer.z = 0;                    // stay on the 2D plane
            Vector3 direction = awayFromPlayer.normalized;
            objects[i].position += direction * speed * Time.deltaTime;
        }
    }

    // ================= HELPERS =================

    private void PickNewWanderPoint(int i)
    {
        Vector3 offset = Random.insideUnitCircle * wanderRadius;
        objectWanderPoints[i] = objectHomes[i] + offset;
    }

    private void SetObjectsVisible(bool visible)
    {
        foreach (Renderer[] renderers in objectRenderers)
        {
            foreach (Renderer r in renderers)
            {
                if (r != null)
                    r.enabled = visible;
            }
        }
    }

    // ALOOF: each object plays a sound.
    //  1. If the object's own Audio Source has a clip set, it plays THAT clip.
    //  2. Otherwise it plays a random clip from the shared aloofSounds list.
    //  3. If neither exists, that object stays silent.
    private void PlayObjectSounds()
    {
        bool hasSharedSounds = aloofSounds != null && aloofSounds.Length > 0;

        foreach (AudioSource source in objectAudio)
        {
            AudioClip clip = source.clip;                // the object's own sound

            if (clip == null && hasSharedSounds)
                clip = aloofSounds[Random.Range(0, aloofSounds.Length)];

            if (clip != null)
                source.PlayOneShot(clip);
        }
    }
}
