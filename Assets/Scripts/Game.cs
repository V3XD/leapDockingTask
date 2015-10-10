using System;
using System.Collections;
using System.IO;
using Leap;
using UnityEngine;

public class Game : MonoBehaviour 
{
    public GameObject cursor;
    public GameObject target;
    public Material green;
    public Material yellow;
    public AudioClip popSound;
    public AudioSource popSource;
    public GUIText pointText;
    public GUIText instructionsText;
    public Material transGreen;
    public Material transYellow;
    public GameObject posCube;
    public GameObject pointer;
    public Light roomLight;
    public AudioSource bassSource;
    public AudioSource drumsSource;

    static float scale = 0.1f; //mm to cm
    static float maxTime = 40f; //max time before the trial is skipped
    static int totalTrials = 6;
    static float cap = 10.0f;
    static float minDistance = cap - 2.0f;
    static float thresholdDistance = 1.5f;
    Controller controller;
    Frame currentFrame;
    Frame prevFrame;
    Vector3 currentPosition = new Vector3();
    Vector3 prevPosition = new Vector3();
    bool isFirst = true;
    bool isDocked = false;
    string folderPath;
    float initDistance;
    float finalDistance;
    float prevTotalTime = 0;
    int trialCount = 0;
    string color = "white";
    bool skipWindow = false;
    int timer = 0;
    bool endWindow = false;
    bool initPinch = true;

    void OnGUI()
    {
        if (!instructionsText.enabled && !skipWindow && !endWindow)
        {
            timer = (int)(Time.time - prevTotalTime);
        }

        GUI.Box(new Rect(UnityEngine.Screen.width - 200, 0, 200, 100), 
                "<size=36>Trial: " + trialCount + "/" + totalTrials + "\n<color=" + color + ">Time: " + timer + "</color> </size>");
        if (skipWindow)
        {
            GUI.Window(1, new Rect((UnityEngine.Screen.width * 0.5f) - 105, (UnityEngine.Screen.height * 0.5f) - 50, 210, 100), 
                       DoWindow, "<size=28>Time is up</size>");
        }
        else if (endWindow) 
        {
            GUI.Window(0, new Rect((UnityEngine.Screen.width * 0.5f) - 105, (UnityEngine.Screen.height * 0.5f) - 50, 230, 100), 
                       DoWindow, "<size=28>Trials complete</size>");
        }
    }
    
    void Awake()
    {
        controller = new Controller();
        CreateFile();
    }

    void Start() 
    {
        Cursor.visible = false; 
        SetPosition();
        if (controller.IsConnected) 
        {
            prevFrame = controller.Frame();
        }

        prevTotalTime = Time.time;
    }

    void Update() 
    {
        if (Input.GetKeyUp(KeyCode.Escape)) 
        {
            Cursor.visible = true; 
            Application.Quit();
        }

        if (instructionsText.enabled && !isFirst) 
        {
            instructionsText.enabled = false;
            prevTotalTime = Time.time;
        }

        if (pointText.enabled) 
        {
            if ((int)(Time.time - prevTotalTime) > 1)
            {
                pointText.enabled = false;
            }
        }

        if (!instructionsText.enabled && !endWindow)
        {
            float currentTime = Time.time - prevTotalTime;
            if (currentTime > maxTime - 10f)
            {
                color = "red";
            }

            if (currentTime > maxTime) 
            {
                skipWindow = true;
            }
        }

        if (controller.IsConnected) 
        {
            currentFrame = controller.Frame();
            if (currentFrame.Id != prevFrame.Id) 
            {
                Tool pointable = currentFrame.Tools.Frontmost;
                HandList hands = currentFrame.Hands;
                Hand hand = hands[0];
                pointer.GetComponent<Renderer>().material = yellow;
                if (hand.PinchStrength == 1)
                {
                    pointer.GetComponent<Renderer>().material = green;
                    if (initPinch)
                    {
                        if (skipWindow)
                        {
                            SetPosition();
                            skipWindow = false;
                            color = "white";
                            popSource.PlayOneShot(popSound);
                            prevTotalTime = Time.time;
                        }
                        else if (endWindow)
                        {
                            popSource.PlayOneShot(popSound);
                            Application.LoadLevel(Application.loadedLevel);
                        }
                        else if (isDocked)
                        {
                            popSource.PlayOneShot(popSound);
                            float trialTime = Time.time - prevTotalTime;
                            File.AppendAllText(folderPath + ".csv", trialTime.ToString() + "," + initDistance.ToString()
                                               + "," + finalDistance.ToString() + Environment.NewLine);
                            SetPosition();
                            trialCount++;
                            pointText.enabled = true;
                            color = "white";
                            if (trialCount >= totalTrials)
                            {
                                endWindow = true;
                            }

                            prevTotalTime = Time.time;
                        }
                    }

                    initPinch = false;
                }
                else
                {
                    initPinch = true;
                }

                if (pointable.IsValid && !skipWindow && !endWindow) 
                {
                    Vector stabilizedPosition = pointable.StabilizedTipPosition;
                    currentPosition = new Vector3(stabilizedPosition.x * scale,
                                                  stabilizedPosition.y * scale,
                                                  -stabilizedPosition.z * scale);
                    Vector directionLeap = pointable.Direction;
                    Vector3 direction = new Vector3(directionLeap.x,
                                                    directionLeap.y,
                                                    -directionLeap.z);
                    pointer.transform.LookAt(direction.normalized + pointer.transform.position);
                    if (!isFirst) 
                    {
                        Vector3 transVec = currentPosition - prevPosition;
                        cursor.transform.Translate(transVec);
                        cursor.transform.position = new Vector3(Mathf.Clamp(cursor.transform.position.x, -cap, cap),
                                                                Mathf.Clamp(cursor.transform.position.y, -cap, cap),
                                                                Mathf.Clamp(cursor.transform.position.z, -cap, cap));
                    }

                    isFirst = false;
                    prevPosition = currentPosition;
                } 
                else 
                {
                    isFirst = true;
                }

                EvaluateDock();
            }
        }
    }
    
    void OnDestroy() 
    {
        controller.Dispose();
    }
    
    void SetPosition()
    { 
        Vector3 position = new Vector3();
        initDistance = 50.0f;
        while (initDistance < minDistance || initDistance > 10)
        {
            position = new Vector3(UnityEngine.Random.Range(-cap, cap),
                                   UnityEngine.Random.Range(-cap, cap),
                                   UnityEngine.Random.Range(-cap, cap));
            initDistance = Vector3.Distance(position, target.transform.position);
        }

        cursor.transform.position = position;
    }
    
    void EvaluateDock()
    {
        Vector3 targetV = target.transform.position;
        Vector3 cursorV = cursor.transform.position;
        finalDistance = Vector3.Distance(targetV, cursorV);
        float distRatio = 1f - (finalDistance / thresholdDistance);

        if (finalDistance <= thresholdDistance)
        {
            isDocked = true;
            transGreen.color = new Vector4(0, distRatio, 0, 1);
            posCube.GetComponent<Renderer>().material = transGreen;
            float intense = ((distRatio * 0.5f) * 4f) + 1f;
            roomLight.intensity = intense;
            drumsSource.volume = 1f - (finalDistance / thresholdDistance);
            bassSource.volume = 1f;
        }
        else
        {
            bassSource.volume = 0;
            drumsSource.volume = 0;
            isDocked = false;
            roomLight.intensity = 1.0f;
            posCube.GetComponent<Renderer>().material = transYellow;
        }
    }
    
    void CreateFile()
    {
        folderPath = @"Log\" + System.DateTime.Now.ToString("MM-dd-yy_hh-mm-ss");
        string columns = "Time,initDistance,finalDistance";
        System.IO.Directory.CreateDirectory(@"Log\");
        File.AppendAllText(folderPath + ".csv", columns + Environment.NewLine);
    }

    void DoWindow(int windowID)
    {
        if (windowID == 0)
        {
            if (GUI.Button(new Rect(70, 45, 95, 30), "<size=20>Restart</size>"))
            {
                Application.LoadLevel(Application.loadedLevel);
            }
        }
        else
        {
            if (GUI.Button(new Rect(50, 45, 95, 30), "<size=20>Skip</size>"))
            {
                SetPosition();
                skipWindow = false;
                color = "white";
                prevTotalTime = Time.time;
            }
        }
    }
}