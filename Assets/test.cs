using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.WebRTC;
using WebSocketSharp;
using System;
using System.Text;
using UnityEngine.UI;

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
    public RTCSessionDescription offer;
    public RTCSessionDescription answer;
    public RTCIceCandidate candidate;
    public string mid;
}


public class test : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private RawImage sourceImage;

    private DelegateOnIceCandidate connIceCandidate;

    // Start is called before the first frame update
    private string IP = "192.168.1.143";
    private string PORT = "8080";

    public WebSocket connection = null;

    private RTCPeerConnection conn;
    private MediaStream videoStream;
    private string target;


    private static RTCConfiguration pc_config()
    {
        RTCConfiguration config = default;
        config.iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } };

        return config;
    }

    private void Awake()
    {
        WebRTC.Initialize(WebRTCSettings.LimitTextureSize);
    }
    private Data onLogin()
    {
        Data data = new Data();
        data.type = "join_room";
        data.name = "user";
        data.room = "test";
        data.mid = "0001";
        return data;
    }
    void Start()
    {
        try
        {
            connection = new WebSocket("ws://" + IP + ":" + PORT);
            connection.OnMessage += Recv;
            connection.OnClose += CloseConnection;
            connection.Connect();
            SendTo(onLogin());
        }
        catch (Exception)
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

    private void OpenConnection(object sender, CloseEventArgs e)
    {
        Debug.Log("소켓 서버와 연결되었습니다.");
    }

    private void CloseConnection(object sender, CloseEventArgs e)
    {
        DisconnectServer();
    }

    public void DisconnectServer()
    {
        Debug.Log("DisconnectServer");
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
        MainThreadHelper.AddAction(() =>
        {
            try
            {
                Data recvData = JsonUtility.FromJson<Data>(e.Data);
                switch (recvData.type)
                {
                    case "login":
                        handleLogin(recvData.success);
                        break;
                    case "offer":
                        handleOffer(recvData.offer, recvData.name);
                        break;
                    case "answer":
                        handleAnswer(recvData.answer);
                        break;
                    case "candidate":
                        handleCandidate(recvData.candidate);
                        break;
                    case "leave":
                        handleLeave();
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogError($"Message: {error.Message}\nStackTrace: {error.StackTrace}");
                throw error;
            }
        });
    }

    public void handleLogin(bool success)
    {
        if(!success)
        {
            Debug.Log("로그인 실패 다시 시도해주세요");
        } else
        {
            Debug.Log("로그인 성공!!");
            var configuration = pc_config();

            conn = new RTCPeerConnection(ref configuration);

            if (videoStream == null)
            {
                videoStream = cam.CaptureStream(WebRTCSettings.StreamSize.x, WebRTCSettings.StreamSize.y, 1000000);
            }

            sourceImage.texture = cam.targetTexture;
            sourceImage.color = Color.white;

            // add Track
            // foreach (var track in videoStream.GetTracks())
            // {
            // conn.AddTrack(track, videoStream);
            // }

            conn.OnIceCandidate = e =>
            {
                Debug.Log("Candidate");
                Debug.Log(e.Candidate);
            };
        }
    }

    public void handleOffer(RTCSessionDescription offer, string name)
    {
        Debug.Log("offer");
        Debug.Log(offer);
        Debug.Log("name");
        Debug.Log(name);
        Debug.Log("유저가 오퍼를 받음");
        target = name;
        conn.SetRemoteDescription(ref offer);
        Debug.Log("유저가 offer를 setRemoteDescription에 저장 했습니다");

        var answer = conn.CreateAnswer();
        onCreateAnswerSuccess(conn, answer.Desc);
    }

    void onCreateAnswerSuccess(RTCPeerConnection pc, RTCSessionDescription answer)
    {
        pc.SetLocalDescription(ref answer);
        Debug.Log("유저가 만든 answer을 setLocalDescription에 저장");
        Data data = new Data();
        data.type = "answer";
        data.answer = answer;
        data.room = "test";
        SendTo(data);
    }

    public void handleAnswer(RTCSessionDescription answer)
    {
        //
    }

    public void handleCandidate(RTCIceCandidate candidate)
    {
        //
        conn.OnIceCandidate(candidate);
        Debug.Log("유저 ICE candidate 등록");
    }

    public void handleLeave()
    {
        //
    }


    public void SendTo(Data message)
    {
        if (!connection.IsAlive)
        {
            return;
        }

        try
        {

            string msg = JsonUtility.ToJson(message);
            connection.Send(msg);
        }
        catch (Exception)
        {
            throw;
        }
    }

}
