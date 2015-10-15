using UnityEngine;
using System;
using System.Collections;
using System.IO;
using Leap;

public class Game : MonoBehaviour 
{
    public GameObject CursorChair;
    public GameObject Target;
    public Material Green;
    public Material Yellow;
    public AudioClip PopSound;
    public AudioSource PopSource;
    public GUIText PointText;
    public GUIText InstructionsText;
    public Material TransGreen;
    public Material TransYellow;
    public GameObject PosCube;
    public GameObject Pointer;
    public Light RoomLight;
    public AudioSource BassSource;
    public AudioSource DrumsSource;

    private static float scale = 0.1f; // mm to cm
    private static float maxTime = 40f; // max time before the trial is skipped
    private static int totalTrials = 6;
    private static float cap = 10.0f;
    private static float minDistance = cap - 2.0f;
    private static float thresholdDistance = 1.5f;
    private Controller controller;
    private Frame currentFrame;
    private Frame prevFrame;
    private Vector3 currentPosition = new Vector3();
    private Vector3 prevPosition = new Vector3();
    private bool isFirst = true;
    private bool isDocked = false;
    private string folderPath;
    private float initDistance;
    private float finalDistance;
    private float prevTotalTime = 0;
    private int trialCount = 0;
    private string color = "white";
    private bool skipWindow = false;
    private int timer = 0;
    private bool endWindow = false;
    private bool initPinch = true;

    private void OnGUI()
    {
        if (!InstructionsText.enabled && !skipWindow && !endWindow)
        {
            timer = (int)(Time.time - prevTotalTime);
        }

        GUI.Box(
            new Rect(UnityEngine.Screen.width - 200, 0, 200, 100), 
            "<size=36>Trial: " + trialCount + "/" + totalTrials + "\n<color=" + color + ">Time: " + timer + "</color> </size>");
        if (skipWindow)
        {
            GUI.Window(
                1, 
                new Rect(
                    (UnityEngine.Screen.width * 0.5f) - 105, 
                    (UnityEngine.Screen.height * 0.5f) - 50, 
                    210, 
                    100), 
                DoWindow, 
                "<size=28>Time is up</size>");
        }
        else if (endWindow) 
        {
            GUI.Window(
                0, 
                new Rect(
                        (UnityEngine.Screen.width * 0.5f) - 105, 
                        (UnityEngine.Screen.height * 0.5f) - 50, 
                        230, 
                        100), 
                DoWindow,
                "<size=28>Trials complete</size>");
        }
    }

    private void Awake()
    {
        controller = new Controller();
        CreateFile();
    }

    private void Start() 
    {
        Cursor.visible = false; 
        SetPosition();
        if (controller.IsConnected) 
        {
            prevFrame = controller.Frame();
        }

        prevTotalTime = Time.time;
    }

    private void Update() 
    {
        if (Input.GetKeyUp(KeyCode.Escape)) 
        {
            Cursor.visible = true; 
            Application.Quit();
        }

        if (InstructionsText.enabled && !isFirst) 
        {
            InstructionsText.enabled = false;
            prevTotalTime = Time.time;
        }

        if (PointText.enabled) 
        {
            if ((int)(Time.time - prevTotalTime) > 1)
            {
                PointText.enabled = false;
            }
        }

        if (!InstructionsText.enabled && !endWindow)
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
                Pointer.GetComponent<Renderer>().material = Yellow;
                if (hand.PinchStrength == 1)
                {
                    Pointer.GetComponent<Renderer>().material = Green;
                    if (initPinch)
                    {
                        if (skipWindow)
                        {
                            SetPosition();
                            skipWindow = false;
                            color = "white";
                            PopSource.PlayOneShot(PopSound);
                            prevTotalTime = Time.time;
                        }
                        else if (endWindow)
                        {
                            PopSource.PlayOneShot(PopSound);
                            Application.LoadLevel(Application.loadedLevel);
                        }
                        else if (isDocked)
                        {
                            PopSource.PlayOneShot(PopSound);
                            float trialTime = Time.time - prevTotalTime;
                            File.AppendAllText(
                                folderPath + ".csv",
                                trialTime.ToString() + "," + initDistance.ToString() + "," + finalDistance.ToString() + Environment.NewLine);
                            SetPosition();
                            trialCount++;
                            PointText.enabled = true;
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
                    currentPosition = new Vector3(
                        stabilizedPosition.x * scale,
                        stabilizedPosition.y * scale,
                        -stabilizedPosition.z * scale);
                    Vector directionLeap = pointable.Direction;
                    Vector3 direction = new Vector3(
                        directionLeap.x,
                        directionLeap.y,
                        -directionLeap.z);
                    Pointer.transform.LookAt(direction.normalized + Pointer.transform.position);
                    if (!isFirst) 
                    {
                        Vector3 transVec = currentPosition - prevPosition;
                        CursorChair.transform.Translate(transVec);
                        CursorChair.transform.position = new Vector3(
                            Mathf.Clamp(CursorChair.transform.position.x, -cap, cap),
                            Mathf.Clamp(CursorChair.transform.position.y, -cap, cap),
                            Mathf.Clamp(CursorChair.transform.position.z, -cap, cap));
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

    private void OnDestroy() 
    {
        controller.Dispose();
    }

    private void SetPosition()
    { 
        Vector3 position = new Vector3();
        initDistance = 50.0f;
        while (initDistance < minDistance || initDistance > 10)
        {
            position = new Vector3(
                UnityEngine.Random.Range(-cap, cap),
                UnityEngine.Random.Range(-cap, cap),
                UnityEngine.Random.Range(-cap, cap));
            initDistance = Vector3.Distance(position, Target.transform.position);
        }

        CursorChair.transform.position = position;
    }

    private void EvaluateDock()
    {
        Vector3 targetV = Target.transform.position;
        Vector3 cursorV = CursorChair.transform.position;
        finalDistance = Vector3.Distance(targetV, cursorV);
        float distRatio = 1f - (finalDistance / thresholdDistance);

        if (finalDistance <= thresholdDistance)
        {
            isDocked = true;
            TransGreen.color = new Vector4(0, distRatio, 0, 1);
            PosCube.GetComponent<Renderer>().material = TransGreen;
            float intense = ((distRatio * 0.5f) * 4f) + 1f;
            RoomLight.intensity = intense;
            DrumsSource.volume = 1f - (finalDistance / thresholdDistance);
            BassSource.volume = 1f;
        }
        else
        {
            BassSource.volume = 0;
            DrumsSource.volume = 0;
            isDocked = false;
            RoomLight.intensity = 1.0f;
            PosCube.GetComponent<Renderer>().material = TransYellow;
        }
    }

    private void CreateFile()
    {
        folderPath = @"Log\" + System.DateTime.Now.ToString("MM-dd-yy_hh-mm-ss");
        string columns = "Time,initDistance,finalDistance";
        System.IO.Directory.CreateDirectory(@"Log\");
        File.AppendAllText(folderPath + ".csv", columns + Environment.NewLine);
    }

    private void DoWindow(int windowID)
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