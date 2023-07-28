using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.WebRTC;
using WebSocketSharp;
using System;
using System.Text;

internal static class WebRTCSettings
{
    public const int DefaultStreamWidth = 1280;
    public const int DefaultStreamHeight = 720;

    private static bool s_limitTextureSize = true;
    private static Vector2Int s_StreamSize = new Vector2Int(DefaultStreamWidth, DefaultStreamHeight);
    private static RTCRtpCodecCapability s_useVideoCodec = null;

    public static bool LimitTextureSize
    {
        get { return s_limitTextureSize; }
        set { s_limitTextureSize = value; }
    }

    public static Vector2Int StreamSize
    {
        get { return s_StreamSize; }
        set { s_StreamSize = value; }
    }

    public static RTCRtpCodecCapability UseVideoCodec
    {
        get { return s_useVideoCodec; }
        set { s_useVideoCodec = value; }
    }
}

[System.Serializable]
public class Data
{
    public string type;
    public string name;
    public string room;
    public bool success;
}


public class test : MonoBehaviour
{
    [SerializeField] private Camera cam;

    // Start is called before the first frame update
    private string IP = "192.168.1.143";
    private string PORT = "8080";

    public WebSocket connection = null;

    private RTCPeerConnection conn;
    private MediaStream videoStream;
    private string target;


    private void Awake()
    {
        WebRTC.Initialize(WebRTCSettings.LimitTextureSize);
    }
    void Start()
    {
        try
        {
            connection = new WebSocket("ws://" + IP + ":" + PORT);
            // connection = new WebSocket("ws://192.168.1.143:8080");
            connection.Connect();
            Debug.Log("소켓 서버와 연결 되었습니다");
            connection.OnMessage += Recv;
            connection.OnClose += CloseConnection;
        } catch(Exception)
        {
            throw;
        }
    }

    public void Connect()
    {
        try
        {
            if(connection == null || !connection.IsAlive)
            {
                connection.Close();
            }
        } catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }

    private void CloseConnection(object sender, CloseEventArgs e)
    {
        DisconnectServer();
    }

    public void DisconnectServer()
    {
        try
        {
            if(connection == null)
            {
                return;
            }

            if(connection.IsAlive)
            {
                connection.Close();
            }
        } catch (Exception)
        {
            throw;
        }
    }



    public void Recv(object sender, MessageEventArgs e)
    {
        Debug.Log("Message Received from " + ((WebSocket)sender).Url + ", Data : " + e.Data);
        Data recvData = JsonUtility.FromJson<Data>(e.Data);
        switch(recvData.type)
        {
            case "login":
                handleLogin(recvData.success);
            break;
        }
    }

    public void handleLogin(bool success)
    {
        if(!success)
        {
            Debug.Log("로그인 실패 다시 시도해주세요");
        } else
        {
            Debug.Log("로그인 성공!!");
            conn = new RTCPeerConnection();


            // get Track
            if (videoStream == null)
            {
                videoStream = cam.CaptureStream(WebRTCSettings.StreamSize.x, WebRTCSettings.StreamSize.y, 1000000);
            }

            // add Track
            foreach (var track in videoStream.GetTracks())
            {
                conn.AddTrack(track, videoStream);
            }


        }
    }

    public void handleOffer(RTCSessionDescription offer, string name)
    {
        if(!(connection.IsAlive && conn == null))
        {

        }

    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            Data data = new Data();
            data.type = "join_room";
            data.name = "user";
            data.room = "test";
            SendTo(data);
        }
    }

    public void SendTo(Data message)
    {
        if (!connection.IsAlive)
        {
            return;
        }

        try
        {
            if(target != null)
            {
                message.name = target;
            }
            string msg = JsonUtility.ToJson(message);
            connection.Send(msg);

        }
        catch (Exception)
        {
            throw;
        }
    }

}
