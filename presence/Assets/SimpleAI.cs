using UnityEngine;

public class SimpleAI : MonoBehaviour
{
    public float minWaitTime = 0.5f;
    public float maxWaitTime = 3.2f;
    public enum Emotion { Happy, Distraught, Sad, Angry, Aloof };

    public Emotion currentState;

    private float timer;
    private float waitTime;

    void Start()
    {
        currentState = Emotion.Happy;
        timer = 0;
        waitTime = Random.Range(minWaitTime, maxWaitTime);
    }


    void Update()
    {
        // Time.deltatime == how long this frame took to render
        // or, how long has passed since the PREVIOUS frame?
        timer += Time.deltaTime;

        switch (currentState)
        {
            case Emotion.Happy:
                OnHappy();
                break;
            case Emotion.Sad:
                OnSad();
                break;
            case Emotion.Angry:
                OnAngry();
                break;
            case Emotion.Aloof:
                OnAloof();
                break;
        }

        // update emotion!!!
        if (timer > waitTime)
        {
            timer = 0;
            waitTime = Random.Range(minWaitTime, maxWaitTime);
            int randomNumber = Random.Range(0, System.Enum.GetNames(typeof(Emotion)).Length);
            currentState = (Emotion)randomNumber;
        }
    }


    private void OnHappy()
    {
        Debug.Log("i am happy");
    }

    private void OnSad()
    {
        Debug.Log("i am sad");
    }


    private void OnAngry()
    {
        Debug.Log("i am angry");
    }


    private void OnAloof()
    {
        Debug.Log("i am aloof");
    }
}